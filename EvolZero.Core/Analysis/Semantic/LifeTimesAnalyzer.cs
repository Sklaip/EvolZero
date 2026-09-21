using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Core.Tools;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EvolZero.Core.Analysis.Semantic
{
	public class LifeTime
	{
		public Expression Expr { get; set; }
		public VarMeta? VarData { get; set; }
		public int BlockNum { get; set; }
		public bool IsLocal { get; set; }
		public bool IsAnonymous { get; set; }
		public bool IsStrippedViaExchange { get; set; }
	}

	public class VarMeta(int blockNum, bool isDestructed, bool isInitialized, List<VarMeta>? isAliseTo)
	{
		public int BlockNum { get; set; } = blockNum;
		public bool IsDestructed { get; set; } = isDestructed;
		public bool IsInitialized { get; set; } = isInitialized;
		public List<VarMeta> Aliases { get; set; } = new();
		public List<VarMeta>? IsAliaseTo { get; set; } = isAliseTo;
	}

	public class LifeTimesAnalyzer : SemanticTreeVisitor<LifeTime?>
	{
		public const string LIFETIMES_LAYER = "LifeTimesAnalyzer";

		private int _currentBlockNum = -1; // -1 чтобы был 0, потому что при первом входе в HandleStatemetChilds будет инкремент
		private Dictionary<string, VarMeta> _vars = new();
		private Stack<CurrentBlock> _currentBlocks = new();
		private readonly ErrorsBag _errorsBag;

		/// <summary>
		/// Стек анализа конструкций if/else. Позволяет изолировать состояние переменных
		/// между параллельными ветками (if/else-if/else): каждая ветка анализируется от
		/// общего состояния на входе, а после всей конструкции средства совмещаются.
		/// </summary>
		private Stack<IfAnalysis> _ifAnalyses = new();

		class IfAnalysis
		{
			public Dictionary<string, VarMetaState> Snapshot { get; } = new();
			public HashSet<string> DestructedInAnyBranch { get; } = new();
		}

		class VarMetaState
		{
			public readonly VarMeta Var;
			public readonly int BlockNum;
			public readonly bool IsDestructed;
			public readonly bool IsInitialized;
			public readonly List<VarMeta>? IsAliaseTo;
			public readonly int IsAliaseToCount;
			public readonly List<VarMeta> Aliases;
			public readonly int AliasesCount;

			public VarMetaState(VarMeta meta)
			{
				Var = meta;
				BlockNum = meta.BlockNum;
				IsDestructed = meta.IsDestructed;
				IsInitialized = meta.IsInitialized;
				IsAliaseTo = meta.IsAliaseTo;
				IsAliaseToCount = meta.IsAliaseTo?.Count ?? 0;
				Aliases = meta.Aliases;
				AliasesCount = meta.Aliases.Count;
			}

			public void Restore()
			{
				Var.BlockNum = BlockNum;
				Var.IsDestructed = IsDestructed;
				Var.IsInitialized = IsInitialized;

				if (IsAliaseTo == null)
				{
					Var.IsAliaseTo = null;
				}
				else
				{
					Var.IsAliaseTo = IsAliaseTo;
					Truncate(IsAliaseTo, IsAliaseToCount);
				}

				Truncate(Aliases, AliasesCount);
			}

			private static void Truncate(List<VarMeta> list, int count)
			{
				if (list.Count > count) list.RemoveRange(count, list.Count - count);
			}
		}

		private VarMeta? _currentClass;
		private Dictionary<string, VarMeta>? _currentClassFields;

		private ILifitemesBypassConsumer _lifetimesConsumer = new LiftimesConsumer();

		class CurrentBlock(Statement codeBlock)
		{
			public Statement CodeBlock { get; } = codeBlock;
			public List<LifeTime> Vars { get; } = new();
		}

		public LifeTimesAnalyzer(ErrorsBag errorsBag)
		{
			_errorsBag = errorsBag;
		}

		protected override void HandleClass(ClassStatement statement)
		{
			_currentClass = new VarMeta(-1, false, true, null);

			_currentClassFields = statement.TypeDesc.Variables.Values.Select(x => (x.Name, new VarMeta(-1, false, false, null)
			{
				Aliases = [_currentClass]
			}
			)).ToDictionary();

			_currentClass.IsAliaseTo = _currentClassFields.Values.ToList();

			base.HandleClass(statement);

			_currentClass = null;
			_currentClassFields = null;
		}

		protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
		{
			base.HandleFunctionalBlock(statement);

			_currentBlocks = new();
			_vars = new();
			_currentBlockNum = -1;
		}

		protected override void HandleStatemetChilds(Statement statement)
		{
			_currentBlocks.Push(new(statement));
			_currentBlockNum++;

			if (statement is IFunctionalBlockStatement fst)
			{
				foreach (var argument in fst.Arguments)
				{
					var liftime = argument.Declaring.IsBorrowRef ? (_currentBlockNum - 1) : _currentBlockNum;
					var variable = new VarMeta(liftime, false, true, null);
					_vars.Add(argument.Name, variable);

					var lifetime = new LifeTime()
					{
						Expr = new VariableAccessExpression(argument.Name, argument.Declaring, true, statement.Pos),
						BlockNum = liftime,
						VarData = variable
					};

					_currentBlocks.Peek().Vars.Add(lifetime);
				}
			}

			base.HandleStatemetChilds(statement);

			_currentBlockNum--;
			_currentBlocks.Pop();
		}

		protected override void HandleIfStatement(IfStatement statement)
		{
			_lifetimesConsumer.EnterToIfStatement(statement);

			var analysis = new IfAnalysis();
			foreach (var (name, meta) in _vars)
			{
				analysis.Snapshot[name] = new VarMetaState(meta);
			}

			_ifAnalyses.Push(analysis);

			try
			{
				base.HandleIfStatement(statement);
				MergeDestructedState(analysis);
			}
			finally
			{
				_ifAnalyses.Pop();
				_lifetimesConsumer.ExitFromIfStatement(statement);
			}
		}

		/// <summary>Возвращает переменные к состоянию на входе в конструкцию if/else, чтобы ветки не видели изменения соседних.</summary>
		private void RestoreVariablesToSnapshot()
		{
			var snapshot = _ifAnalyses.Peek().Snapshot;
			foreach (var (name, state) in snapshot)
			{
				state.Restore();
				_vars[name] = state.Var;
			}
		}

		/// <summary>Запоминает, какие переменные были деинициализированы в только что обработанной ветке.</summary>
		private void CollectDestructedVariables()
		{
			var analysis = _ifAnalyses.Peek();
			foreach (var name in analysis.Snapshot.Keys)
			{
				if (_vars.TryGetValue(name, out var meta) && meta.IsDestructed)
					analysis.DestructedInAnyBranch.Add(name);
			}
		}

		/// <summary>
		/// После обработки всех веток любая переменная, деинициализированная хотя бы в одной
		/// ветке, считается деинициализированной и после всей конструкции if/else.
		/// </summary>
		private void MergeDestructedState(IfAnalysis analysis)
		{
			foreach (var name in analysis.DestructedInAnyBranch)
			{
				if (_vars.TryGetValue(name, out var meta))
					meta.IsDestructed = true;
			}
		}

		protected override void HandleIfChilds(IfStatement statement)
		{
			RestoreVariablesToSnapshot();
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleIfChilds(statement);
			CollectDestructedVariables();
		}

		protected override void HandleElseIfChilds(IfStatement statement)
		{
			RestoreVariablesToSnapshot();
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleElseIfChilds(statement);
			CollectDestructedVariables();
		}

		protected override void HandleElseChilds(Statement statement)
		{
			RestoreVariablesToSnapshot();
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleElseChilds(statement);
			CollectDestructedVariables();
		}

		protected override void SubTreeEnd(LifeTime? value)
		{
			if (value == null) return;

			if (value.IsAnonymous && value.Expr.ResultTypeSpec.IsOwnerRef)
			{
				//ToDestructPointer(value);
			}
		}

		protected override LifeTime AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsAnonymous = true
			};
		}

		protected override LifeTime CallConstructor(CallConstructorExpression expr)
		{
			base.CallConstructor(expr);
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsAnonymous = true
			};
		}

		protected override LifeTime GetPointerToVar(GetPointerToVarExpression expr)
		{
			base.GetPointerToVar(expr);
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsAnonymous = false
			};
		}

		protected override LifeTime AppealToThis(AppealToThisExpression expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = 0,
				IsAnonymous = true,
				VarData = _currentClass
			};
		}

		protected override LifeTime CreateGlobalArray(GlobalArrayExpression expr)
		{
			// TODO: вроде array называется global, а вроде мы его потом удалять будем. Хуета какая-то
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsAnonymous = false
			};
		}

		protected override LifeTime? StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			var structureGetting = HandleExpression(expr.StructureGetting);

			if (structureGetting == null)
				return null; // ошибка уже зарегистрирована (например, обращение к деинициализированной ссылке)

			if (structureGetting.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			VarMeta meta;
			if (structureGetting.Expr is AppealToThisExpression)
			{
				if (_currentClassFields == null)
					throw new NotImplementedException(); // такой хуйни быть не должно
				meta = _currentClassFields[expr.Field.Name];
				meta.IsInitialized = expr.IsInitialized;
			}
			else
			{
				meta = new VarMeta(structureGetting.VarData.BlockNum, false, expr.IsInitialized, null)
				{
					Aliases = [structureGetting.VarData]
				};
			}

			if (!expr.ResultTypeSpec.IsRef)
			{
				return new LifeTime()
				{
					Expr = expr,
					BlockNum = structureGetting.BlockNum,
					VarData = meta
				};
			}

			return new LifeTime()
			{
				Expr = expr,
				BlockNum = structureGetting.BlockNum,
				VarData = meta
			};
		}

		protected override LifeTime? VarAccess(VariableAccessExpression expr)
		{
			_vars.TryGetValue(expr.Name, out var varMeta);

			if (varMeta == null) throw new NotImplementedException();

			if (varMeta.IsDestructed)
			{
				_errorsBag.AddError(LIFETIMES_LAYER, "LT001", $"Нет доступа к деинициализированной ссылке '{expr.Name}'", expr.Pos);
				return null;
			}

			varMeta.IsInitialized = expr.IsInitialized;

			if (!expr.ResultTypeSpec.IsRef)
			{
				return new LifeTime()
				{
					Expr = expr,
					BlockNum = varMeta.BlockNum,
					VarData = varMeta,
					IsLocal = true
				};
			}

			return new LifeTime()
			{
				Expr = expr,
				BlockNum = varMeta.BlockNum,
				VarData = varMeta,
				IsLocal = true
			};
		}

		protected override LifeTime? CallFunction(CallFunctionExpression expr)
		{
			int i = 0;
			int j = 0;

			var acceptedArguments = expr.Function.Arguments;
			var passedArguments = expr.Arguments;

			if (expr.Function.DeclaringType != null)
			{
				HandleExpression(passedArguments[0]);
				j++;
			}

			for (; i < acceptedArguments.Length; i++, j++)
			{
				var arg = HandleExpression(passedArguments[j]);
				if (arg == null) continue;
				PassToArgumentHandler(acceptedArguments[i].Declaring, arg);
			}

			if (!expr.ResultTypeSpec.IsRef) return null;

			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsAnonymous = true
			};
		}

		protected override LifeTime? CreateVar(VariableCreatingExpression expr)
		{
			var currentVar = new VarMeta(_currentBlockNum, false, false, null);

			_vars[expr.Name] = currentVar;
			if (!expr.ResultTypeSpec.IsRef) return null;

			var lifetime = new LifeTime()
			{
				Expr = new VariableAccessExpression(expr.Name, expr.ResultTypeSpec, expr.IsInitialized, expr.Pos),
				BlockNum = _currentBlockNum,
				VarData = currentVar
			};

			_currentBlocks.Peek().Vars.Add(lifetime);

			return lifetime;
		}

		protected override LifeTime? SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
		{
			LifeTime? left = HandleExpression(expr.LeftExpression);
			LifeTime? right = HandleExpression(expr.RightExpression);

			if (left == null || right == null) return null;

			switch (expr.OperationType)
			{
				case BinaryOperation.Assing:
					AssingHandler(left, right);
					return left;
				default:
					return null;
			}
		}

		protected override LifeTime? Exchange(ExchangeExpression expr)
		{
			LifeTime? target = HandleExpression(expr.Target);
			LifeTime? value = HandleExpression(expr.Value);

			if (target == null || value == null) return null;

			if (!target.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return target;

			if (value.BlockNum > target.BlockNum && !value.IsAnonymous)
				throw new NotImplementedException(); // ошибка что время жизни больше времени жизни ссылки

			if (value.VarData != null && (value.VarData.IsDestructed || !value.VarData.IsInitialized))
				throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно поподать не должно, оно должно отбрасываться на других проверках

			var varIsOwner = target.Expr.ResultTypeSpec.IsOwnerRef;

			if (target.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
					throw new NotImplementedException(); // ошибка что во владеющую ссылку нельзя пихать заимствованные значения

				if (value.Expr is StructureFieldAccessExpression)
					throw new NotImplementedException(); // нельзя снимать владеюущие ссылки с классов

				if (target.Expr is StructureFieldAccessExpression fieldAccess)
				{
					target.IsStrippedViaExchange = true;
				}

				if (!value.IsAnonymous)
					_lifetimesConsumer.GiveAwayOwnershipToRef(value.Expr);

				target.BlockNum = value.BlockNum;

				GiveAwayOwnership(value);
			}
			else
			{
				AssignBorrowRef(target, value);
			}

			target.VarData.IsInitialized = true;

			return target;
		}

		protected override LifeTime? HandleReturnStatement(ReturnStatement statement)
		{
			var returnedLifetime = base.HandleReturnStatement(statement);

			if (returnedLifetime?.Expr != null
				&& returnedLifetime.Expr.ResultTypeSpec.IsRef
				&& !returnedLifetime.Expr.ResultTypeSpec.IsOwnerRef)
			{
				throw new NotImplementedException(); // ошибка что возвращаемая ссылка всегда должна быть владеющуй
			}

			DestructLifetimes(returnedLifetime?.VarData);
			return returnedLifetime;
		}

		private void AssingHandler(LifeTime target, LifeTime value)
		{
			if (!target.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return;

			if (value.BlockNum > target.BlockNum && !value.IsAnonymous)
				throw new NotImplementedException(); // ошибка что время жизни больше времени жизни ссылки

			if (value.VarData != null && (value.VarData.IsDestructed || !value.VarData.IsInitialized))
				throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно попадть не должно, оно должно отбрасываться на других проверках

			var varIsOwner = target.Expr.ResultTypeSpec.IsOwnerRef;

			if (target.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
					throw new NotImplementedException(); // ошибка что во владеющую ссылку нельзя пихать заимствованные значения

				if (value.Expr is StructureFieldAccessExpression)
				{
					// снять владеющую ссылку с поля класса можно только через оператор <-,
					// чтобы поле не осталось деинициализированным, а старая ссылка ушла в приемник
					if (!value.IsStrippedViaExchange)
						throw new NotImplementedException(); // нельзя просто так снимать владеющую ссылку с класса

					if (target.BlockNum > value.BlockNum)
						throw new NotImplementedException(); // у приемника время жизни меньше, чем у объекта, с которого снимается ссылка
				}

				if (target.VarData.IsInitialized)
				{
					if (!target.IsLocal)
						throw new NotImplementedException(); // нельзя переназначать уже инициализированные не локальные ссылки (поля класов например)

					ToDestructPointer(target, true); // удалям старую ссылку
				}

				if (!value.IsAnonymous && !value.IsStrippedViaExchange)
					_lifetimesConsumer.GiveAwayOwnershipToRef(value.Expr);

				target.BlockNum = value.BlockNum;

				if (value.IsStrippedViaExchange)
					value.VarData!.IsDestructed = true; // содержимое поля переехало в приемник (GiveAwayOwnership кинул бы throw на алиасы поля)
				else
					GiveAwayOwnership(value);
			}
			else
			{
				AssignBorrowRef(target, value);
			}

			target.VarData.IsInitialized = true;
		}

		private void AssignBorrowRef(LifeTime target, LifeTime value)
		{
			if (value.IsAnonymous)
				throw new NotImplementedException(); // ошибка что анонимные ссылки (напрмиер те что выдаются через new) можно присвивать только во владеющие ссылки

			if (value.VarData != null)
			{
				if (target.VarData == null)
					throw new NotImplementedException(); // такой хуйни быть не должно

				if (target.VarData.IsAliaseTo == null)
					target.VarData.IsAliaseTo = new();

				if (value.VarData.IsAliaseTo != null)
				{
					target.VarData.IsAliaseTo.AddRange(value.VarData.IsAliaseTo);
				}
				else
				{
					target.VarData.IsAliaseTo.Add(value.VarData);
				}

				if (value.VarData.Aliases == null)
					value.VarData.Aliases = new();

				value.VarData.Aliases.Add(target.VarData);
				target.BlockNum = value.BlockNum;
				target.VarData.BlockNum = value.BlockNum;
			}
		}

		private void PassToArgumentHandler(TypeSpec argument, LifeTime value)
		{
			if (!argument.IsRef) return;

			if (argument.IsRef && !value.Expr.ResultTypeSpec.IsRef)
				throw new NotImplementedException(); // рассмотреть эти ситуации. Вроде на уровне семантического древа такого быть не может

			var varIsOwner = argument.IsOwnerRef;

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
					throw new NotImplementedException(); // ошибка что во владеющую ссылку нельзя пихать заимствованные значения

				if (value.Expr is StructureFieldAccessExpression)
					throw new NotImplementedException(); // нельзя снимать владеюущие ссылки с классов. Потом для этого сделать оператор замены или ссылку обнулять

				GiveAwayOwnership(value);
			}
		}

		private void ToDestructPointer(LifeTime pointer, bool checkAliases)
		{
			if (pointer.VarData == null) throw new NotImplementedException(); // такой хуйни быть не должно

			if (checkAliases && pointer.VarData.Aliases != null && pointer.VarData.Aliases.Count > 0)
			{
				throw new NotImplementedException(); // ошибка что нельзя передавать владение ссылкой у которой есть алиасы
			}

			if (!pointer.VarData.IsInitialized || pointer.VarData.IsDestructed || !pointer.Expr.ResultTypeSpec.IsOwnerRef)
				return;

			var expr = _lifetimesConsumer.DestructPointer(pointer.Expr);
			if (expr != null)
			{
				AddToCurrentStatement(expr);
			}
		}

		private void GiveAwayOwnership(LifeTime pointer)
		{
			if (pointer.VarData == null) return;

			if (pointer.VarData.Aliases != null && pointer.VarData.Aliases.Count > 0)
			{
				throw new NotImplementedException(); // ошибка что нельзя передавать владение ссылкой у которой есть алиасы
			}

			pointer.VarData.IsDestructed = true;
		}

		private void DestructLifetimes(VarMeta? excludedVar = null)
		{
			var block = _currentBlocks.Peek();

			foreach (var lifetime in block.Vars)
			{
				if (excludedVar != null && lifetime.VarData == excludedVar) continue;
				ToDestructPointer(lifetime, false);
			}
		}
	}
}
