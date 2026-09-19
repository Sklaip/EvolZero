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
		public bool ToLocalValue { get; set; }
		public bool IsLocal { get; set; }
		public bool IsAnonymous { get; set; }
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
		private int _currentBlockNum = -1; // -1 чтобы был 0, потому что при первом входе в HandleStatemetChilds будет инкремент
		private Dictionary<string, VarMeta> _vars = new();
		private Stack<CurrentBlock> _currentBlocks = new();
		private readonly ErrorsBag _errorsBag;

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
			if (statement.Name == "PassRef")
			{

			}

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
					var variable = new VarMeta(_currentBlockNum, false, true, null);
					_vars.Add(argument.Name, variable);

					var lifetime = new LifeTime()
					{
						Expr = new VariableAccessExpression(argument.Name, argument.Declaring, true, statement.Pos),
						BlockNum = _currentBlockNum,
						VarData = variable,
						ToLocalValue = false
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
			base.HandleIfStatement(statement);
			_lifetimesConsumer.ExitFromIfStatement(statement);
		}

		protected override void HandleIfChilds(IfStatement statement)
		{
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleIfChilds(statement);
		}

		protected override void HandleElseIfChilds(IfStatement statement)
		{
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleElseIfChilds(statement);
		}

		protected override void HandleElseChilds(Statement statement)
		{
			_lifetimesConsumer.HandleConditionSubStatement(statement);
			base.HandleElseChilds(statement);
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
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime CallConstructor(CallConstructorExpression expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime GetPointerToVar(GetPointerToVarExpression expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				ToLocalValue = expr.Variable is VariableCreatingExpression or VariableAccessExpression,
				IsAnonymous = true
			};
		}

		protected override LifeTime AppealToThis(AppealToThisExpression expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = 0,
				ToLocalValue = false,
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
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime? StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			var structureGetting = HandleExpression(expr.StructureGetting);

			if (structureGetting?.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (structureGetting == null) throw new NotImplementedException(); //для дебага. Такого быть не должно

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
				VarData = meta,
				ToLocalValue = false
			};
		}

		protected override LifeTime? VarAccess(VariableAccessExpression expr)
		{
			_vars.TryGetValue(expr.Name, out var varMeta);

			if (varMeta == null) throw new NotImplementedException();

			if (varMeta.IsDestructed)
				throw new NotImplementedException();  // тут ошибка что нельзя обратиться к деинициализированной ссылке

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
				ToLocalValue = false,
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
				ToLocalValue = false,
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
				VarData = currentVar,
				ToLocalValue = false
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

		protected override void HandleReturnStatement(ReturnStatement statement)
		{
			DestructLifetimes();
			base.HandleReturnStatement(statement);
		}

		private void AssingHandler(LifeTime variable, LifeTime value)
		{
			if (!variable.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return;

			if (value.BlockNum > variable.BlockNum && !value.IsAnonymous)
				throw new NotImplementedException(); // ошибка что время жизни больше времени жизни ссылки

			if (value.VarData != null && (value.VarData.IsDestructed || !value.VarData.IsInitialized))
				throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно попадть не должно, оно должно отбрасываться на других проверках

			var varIsOwner = variable.Expr.ResultTypeSpec.IsOwnerRef;

			if (variable.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
					throw new NotImplementedException(); // ошибка что во владеющую ссылку нельзя пихать заимствованные значения

				if (value.Expr is StructureFieldAccessExpression)
					throw new NotImplementedException(); // нельзя снимать владеюущие ссылки с классов. Потом для этого сделать оператор замены или ссылку обнулять

				if (variable.VarData.IsInitialized)
				{
					if (!variable.IsLocal)
						throw new NotImplementedException(); // нельзя переназначать уже инициализированные не локальные ссылки (поля класов например)

					ToDestructPointer(variable); // удалям старую ссылку
				}

				if (!value.IsAnonymous && !variable.ToLocalValue)
					_lifetimesConsumer.GiveAwayOwnershipToRef(value.Expr);

				variable.BlockNum = value.BlockNum;

				GiveAwayOwnership(value);
			}
			else
			{
				if (value.IsAnonymous && !value.ToLocalValue)
					throw new NotImplementedException(); // ошибка что анонимные ссылки (напрмиер те что выдаются через new и loc) можно присвивать только во владеющие ссылки

				if (value.VarData != null)
				{
					if (variable.VarData.IsAliaseTo == null)
						variable.VarData.IsAliaseTo = new();

					if (value.VarData.IsAliaseTo != null)
					{
						variable.VarData.IsAliaseTo.AddRange(value.VarData.IsAliaseTo);
					}
					else
					{
						variable.VarData.IsAliaseTo.Add(value.VarData);
					}

					if (value.VarData.Aliases == null)
						value.VarData.Aliases = new();

					value.VarData.Aliases.Add(variable.VarData);
				}

			}

			variable.ToLocalValue = value.ToLocalValue;
			variable.VarData.IsInitialized = true;
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

		private void ToDestructPointer(LifeTime pointer)
		{
			if (pointer.VarData == null) throw new NotImplementedException(); // такой хуйни быть не должно

			if (pointer.VarData.Aliases != null && pointer.VarData.Aliases.Count > 0)
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

		private void DestructLifetimes()
		{
			var block = _currentBlocks.Peek();

			foreach (var lifetime in block.Vars)
			{
				ToDestructPointer(lifetime);
			}
		}
	}
}
