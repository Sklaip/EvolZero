using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System.Xml.Linq;

namespace EvolZero.Core.Analysis.Semantic
{
	public class ValueMeta
	{
		public Expression Expr { get; set; }
		public VarMeta? VarData { get; set; }
		public bool IsLocal { get; set; }
		public bool IsAnonymous { get; set; }
		public bool IsStrippedViaExchange { get; set; }
	}

	public class VarMeta(int blockNum, bool isDestructed, bool isInitialized)
	{
		public string? Name { get; set; } // TODO: для дебага
		public int LifetimeNum { get; set; } = blockNum; // Чем меньше LifetimeNum (номер лайфтайма), тем больше время жизни
		public bool IsDestructed { get; set; } = isDestructed;
		public bool IsInitialized { get; set; } = isInitialized;
		public List<VarMeta> Aliases { get; set; } = new();
		public List<VarMeta> LinkedVars { get; set; } = new();
	}

	public class LifeTimesAnalyzer : SemanticTreeVisitor<ValueMeta?>
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
			public readonly List<VarMeta> Aliases;
			public readonly List<VarMeta> LinkedVars;
			public readonly int AliasesCount;
			public readonly int LinkedVarsCount;

			public VarMetaState(VarMeta meta)
			{
				Var = meta;
				BlockNum = meta.LifetimeNum;
				IsDestructed = meta.IsDestructed;
				IsInitialized = meta.IsInitialized;
				Aliases = meta.Aliases;
				AliasesCount = meta.Aliases.Count;
				LinkedVars = meta.LinkedVars;
				LinkedVarsCount = meta.LinkedVars.Count;
			}

			public void Restore()
			{
				Var.LifetimeNum = BlockNum;
				Var.IsDestructed = IsDestructed;
				Var.IsInitialized = IsInitialized;

				Truncate(Aliases, AliasesCount);
				Truncate(LinkedVars, LinkedVarsCount);
			}

			private static void Truncate(List<VarMeta> list, int count)
			{
				if (list.Count > count) list.RemoveRange(count, list.Count - count);
			}
		}

		private VarMeta? _currentClass;
		private Dictionary<string, VarMeta>? _currentClassFields;
		private TypeDesc? _currentDesc;
		private DestructorStatement? _currentDestructor;

		/// <summary>
		/// тут находится лайфтайм в который сейчас происходит присваение (AssingHandler)
		/// </summary>
		private ValueMeta? _lifetimeForAssign = null;

		private ILifitemesBypassConsumer _lifetimesConsumer = new LiftimesConsumer();

		class CurrentBlock(Statement codeBlock)
		{
			public Statement CodeBlock { get; } = codeBlock;
			public List<ValueMeta> Vars { get; } = new();
		}

		public LifeTimesAnalyzer(ErrorsBag errorsBag)
		{
			_errorsBag = errorsBag;
		}

		private void AddLifetimeError(string errorCode, string message, PositionInSources pos)
		{
			_errorsBag.AddError(LIFETIMES_LAYER, errorCode, message, pos);
		}

		protected override void HandleClass(ClassStatement statement)
		{
			_currentDesc = statement.TypeDesc;
			_currentClass = new VarMeta(-1, false, true);
			_currentClass.Name = "this";

			_currentClassFields = statement.TypeDesc.Variables.Values.Select(x => (x.Name, new VarMeta(-1, false, false)
			{
				Aliases = [_currentClass],
				Name = x.Name
			}
			)).ToDictionary();

			base.HandleClass(statement);

			_currentClass = null;
			_currentClassFields = null;
			_currentDesc = null;
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
					int liftime = _currentBlockNum;
					if (argument.Declaring.IsRef)
					{
						if (CheckLinkWithThis(fst.Lifetimes, argument.Name)) liftime = _currentClass!.LifetimeNum;
						else if (argument.Declaring.IsBorrowRef) liftime = int.MinValue;
					}

					var variable = new VarMeta(liftime, false, true);
					variable.Name = argument.Name;
					_vars.Add(argument.Name, variable);

					var lifetime = new ValueMeta()
					{
						Expr = new VariableAccessExpression(argument.Name, argument.Declaring, true, statement.Pos),
						VarData = variable
					};

					_currentBlocks.Peek().Vars.Add(lifetime);
				}
			}

			if (statement is DestructorStatement destructor)
			{
				_currentDestructor = destructor;
				base.HandleStatemetChilds(statement);
				_currentDestructor = null;
			}
			else
			{
				base.HandleStatemetChilds(statement);
			}

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

		protected override void SubTreeEnd(ValueMeta? value)
		{
			if (value == null) return;

			if (value.IsAnonymous && value.Expr.ResultTypeSpec.IsOwnerRef)
			{
				//ToDestructPointer(value);
			}
		}

		protected override ValueMeta AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = true
			};
		}

		protected override ValueMeta CallConstructor(CallConstructorExpression expr)
		{
			base.CallConstructor(expr);

			var acceptedArguments = expr.Constructor.Arguments;
			var passedArguments = expr.Arguments;

			var thisGetting = HandleExpression(expr.MemoryGetting);

			for (int i = 0; i < acceptedArguments.Length; i++)
			{
				var arg = HandleExpression(passedArguments[i]);
				if (arg == null) continue;
				PassToArgumentHandler(acceptedArguments[i], arg, expr.Constructor.Lifetimes, thisGetting);
			}

			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = true
			};
		}

		protected override ValueMeta GetPointerToVar(GetPointerToVarExpression expr)
		{
			var lifteime = base.GetPointerToVar(expr);
			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = false
			};
		}

		protected override ValueMeta AppealToThis(AppealToThisExpression expr)
		{
			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = true,
				VarData = _currentClass
			};
		}

		protected override ValueMeta CreateGlobalArray(GlobalArrayExpression expr)
		{
			// TODO: вроде array называется global, а вроде мы его потом удалять будем. Хуета какая-то
			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = false
			};
		}

		protected override ValueMeta? StructureFiledAccess(StructureFieldAccessExpression expr)
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
				meta = new VarMeta(structureGetting.VarData.LifetimeNum, false, expr.IsInitialized)
				{
					Aliases = [structureGetting.VarData]
				};

				meta.Name = expr.Field.Name;
			}

			return new ValueMeta()
			{
				Expr = expr,
				VarData = meta
			};
		}

		protected override ValueMeta? VarAccess(VariableAccessExpression expr)
		{
			_vars.TryGetValue(expr.Name, out var varMeta);

			if (varMeta == null) throw new NotImplementedException();

			if (varMeta.IsDestructed)
			{
				_errorsBag.AddError(LIFETIMES_LAYER, "LT001", $"Нет доступа к деинициализированной ссылке '{expr.Name}'", expr.Pos);
				return null;
			}

			varMeta.IsInitialized = expr.IsInitialized;

			return new ValueMeta()
			{
				Expr = expr,
				VarData = varMeta,
				IsLocal = true
			};
		}

		protected override ValueMeta? CallFunction(CallFunctionExpression expr)
		{
			int i = 0;
			int j = 0;

			var acceptedArguments = expr.Function.Arguments;
			var passedArguments = expr.Arguments;

			// обнуляем текущий лайфтайм в который сейчас происходит присвоение
			var lastAssignLifetime = _lifetimeForAssign; 
			_lifetimeForAssign = null;

			ValueMeta? thisGetting = null; // если это метод класса, щдесь будет лайфтайм this, если обычная функция, то просто null
			if (expr.Function.DeclaringType != null)
			{
				thisGetting = HandleExpression(passedArguments[0]);
				j++;
			}

			for (; i < acceptedArguments.Length; i++, j++)
			{
				var arg = HandleExpression(passedArguments[j]);
				if (arg == null) continue;
				PassToArgumentHandler(acceptedArguments[i], arg, expr.Function.Lifetimes, thisGetting);
			}

			// возвращаем лайфтайм обратно
			_lifetimeForAssign = lastAssignLifetime;

			return new ValueMeta()
			{
				Expr = expr,
				IsAnonymous = true
			};
		}

		protected override ValueMeta? CreateVar(VariableCreatingExpression expr)
		{
			var currentVar = new VarMeta(_currentBlockNum, false, false);
			currentVar.Name = expr.Name;

			_vars[expr.Name] = currentVar;

			var lifetime = new ValueMeta()
			{
				Expr = new VariableAccessExpression(expr.Name, expr.ResultTypeSpec, expr.IsInitialized, expr.Pos),
				VarData = currentVar
			};

			_currentBlocks.Peek().Vars.Add(lifetime);

			return lifetime;
		}

		protected override ValueMeta? SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
		{
			switch (expr.OperationType)
			{
				case BinaryOperation.Assing:
					var lastLifetime = _lifetimeForAssign;
					ValueMeta? left = HandleExpression(expr.LeftExpression);

					if (left == null) return null;

					_lifetimeForAssign = left;
					ValueMeta? right = HandleExpression(expr.RightExpression);
					_lifetimeForAssign = lastLifetime;

					if (right == null) return null;

					AssingHandler(left, right);

					return left;
				default:
					return null;
			}
		}

		protected override ValueMeta? Exchange(ExchangeExpression expr)
		{
			ValueMeta? target = HandleExpression(expr.Target);
			ValueMeta? value = HandleExpression(expr.Value);

			if (target == null || value == null) return null;

			if (!target.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return target;

			if (target.VarData == null)
			{
				throw new NotImplementedException(); // такой хуйни быть не должно
			}

			if (value.VarData != null)
			{
				if (value.VarData.IsDestructed || !value.VarData.IsInitialized)
					throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно поподать не должно, оно должно отбрасываться на других проверках

				if (CheckLifetimesError(value.VarData.LifetimeNum, target.VarData.LifetimeNum) && !value.IsAnonymous)
				{
					ErrorLT002(target.Expr.Pos);
					return null;
				}
			}

			var varIsOwner = target.Expr.ResultTypeSpec.IsOwnerRef;

			if (target.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
				{
					AddLifetimeError("LT003", "Нельзя присвоить заимствованную (refb) ссылку во владеющую (ref) ссылку", value.Expr.Pos);
					return null;
				}

				if (value.Expr is StructureFieldAccessExpression)
				{
					AddLifetimeError("LT004", "Нельзя снимать владеющую (ref) ссылку с поля класса", value.Expr.Pos);
					return null;
				}

				if (target.Expr is StructureFieldAccessExpression fieldAccess)
				{
					target.IsStrippedViaExchange = true;
				}

				if (!value.IsAnonymous)
					_lifetimesConsumer.GiveAwayOwnership(value.Expr);

				GiveAwayOwnership(value, target.VarData.LifetimeNum, true);
			}
			else
			{
				AssignBorrowRef(target, value);
			}

			target.VarData.IsInitialized = true;

			return target;
		}

		protected override ValueMeta? HandleReturnStatement(ReturnStatement statement)
		{
			var returnedLifetime = base.HandleReturnStatement(statement);

			if (returnedLifetime?.Expr != null
				&& returnedLifetime.Expr.ResultTypeSpec.IsRef
				&& !returnedLifetime.Expr.ResultTypeSpec.IsOwnerRef)
			{
				AddLifetimeError("LT005", "Возвращаемая ссылка должна быть владеющей (ref)", returnedLifetime.Expr.Pos);
				return null;
			}

			if (returnedLifetime?.VarData != null && returnedLifetime.VarData.LinkedVars.Count > 0)
			{
				AddLifetimeError("LT012", "Возвращаемая ссылка не должна иметь связанных ссылок!", returnedLifetime.Expr.Pos);
				return null;
			}

			DestructLifetimes(returnedLifetime?.VarData);

			if (_currentDestructor != null)
			{
				foreach (var newStatement in _lifetimesConsumer.HandleDestructor(_currentDesc!, _currentDestructor))
				{
					AddToCurrentStatement(newStatement);
				}
			}

			return returnedLifetime;
		}

		private void AssingHandler(ValueMeta target, ValueMeta value)
		{
			if (!target.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return;

			if (target.VarData == null)
			{
				throw new NotImplementedException(); // такой хуйни быть не должно
			}

			if (value.VarData != null)
			{
				if (value.VarData.IsDestructed || !value.VarData.IsInitialized)
					throw new NotImplementedException(); // ссылка была деинициализированна. Вообще сюда оно попадть не должно, оно должно отбрасываться на других проверках

				if (CheckLifetimesError(value.VarData.LifetimeNum, target.VarData.LifetimeNum) && !value.IsAnonymous)
				{
					ErrorLT002(target.Expr.Pos);
					return;
				}
			}

			var varIsOwner = target.Expr.ResultTypeSpec.IsOwnerRef;

			if (target.VarData == null)
				throw new NotImplementedException(); // такой хуйни быть не должно

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
				{
					AddLifetimeError("LT003", "Нельзя присвоить заимствованную (refb) ссылку во владеющую (ref) ссылку", value.Expr.Pos);
					return;
				}

				if (value.Expr is StructureFieldAccessExpression)
				{
					// снять владеющую ссылку с поля класса можно только через оператор <-,
					// чтобы поле не осталось деинициализированным, а старая ссылка ушла в приемник
					if (!value.IsStrippedViaExchange)
					{
						AddLifetimeError("LT006", "Снять владеющую ссылку с поля класса можно только через оператор '<-'", value.Expr.Pos);
						return;
					}

					if (value.VarData != null && CheckLifetimesError(target.VarData.LifetimeNum, value.VarData.LifetimeNum))
					{
						AddLifetimeError("LT007", "Время жизни ссылки-получателя меньше времени жизни объекта, с которого снимается ссылка", target.Expr.Pos);
						return;
					}
				}

				if (target.VarData.IsInitialized)
				{
					if (!target.IsLocal)
					{
						AddLifetimeError("LT008", "Нельзя переназначать уже инициализированную ссылку, не являющуюся локальной (например, поле класса)", target.Expr.Pos);
						return;
					}

					ToDestructPointer(target, true); // удалям старую ссылку
				}

				if (!value.IsAnonymous && !value.IsStrippedViaExchange)
					_lifetimesConsumer.GiveAwayOwnership(value.Expr);

				if (value.IsStrippedViaExchange)
				{
					value.VarData!.IsDestructed = true; // содержимое поля переехало в приемник (GiveAwayOwnership кинул бы throw на алиасы поля)
					LinkVars(value.VarData, target.VarData);
				}
				else
					GiveAwayOwnership(value, target.VarData.LifetimeNum, true);
			}
			else
			{
				AssignBorrowRef(target, value);
			}

			target.VarData.IsInitialized = true;
		}

		private void AssignBorrowRef(ValueMeta target, ValueMeta value)
		{
			if (value.IsAnonymous)
			{
				AddLifetimeError("LT009", "Анонимные ссылки (например, результат 'new') можно присваивать только во владеющие (ref) ссылки", value.Expr.Pos);
				return;
			}

			if (value.VarData != null)
			{
				if (target.VarData == null)
					throw new NotImplementedException(); // такой хуйни быть не должно

				if (value.VarData.Aliases == null)
					value.VarData.Aliases = new();

				value.VarData.Aliases.Add(target.VarData);

				target.VarData.LifetimeNum = value.VarData.LifetimeNum;
				target.VarData.LifetimeNum = value.VarData.LifetimeNum;
			}
		}

		private void PassToArgumentHandler(Argument arg, ValueMeta value, LifetimeDecl[] functionLifetimes, ValueMeta? objectGetting)
		{
			var argument = arg.Declaring;
			if (value.VarData?.IsDestructed == true)
			{
				_errorsBag.AddError(LIFETIMES_LAYER, "LT001",
					$"Нет доступа к деинициализированной ссылке переданной в аргумент", value.Expr.Pos); // TODO: выводить чо за именно аргумент
				return;
			}

			if (!argument.IsRef) return;

			if (argument.IsRef && !value.Expr.ResultTypeSpec.IsRef)
				throw new NotImplementedException(); // рассмотреть эти ситуации. Вроде на уровне семантического древа такого быть не может

			bool linkWithObjectExists = CheckLinkWithThis(functionLifetimes, arg.Name);

			if (value.VarData != null && linkWithObjectExists
				&& objectGetting?.VarData != null && CheckLifetimesError(value.VarData.LifetimeNum, objectGetting.VarData.LifetimeNum))
			{
				ErrorLT002(value.Expr.Pos);
				return;
			}

			var varIsOwner = argument.IsOwnerRef;

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
				{
					AddLifetimeError("LT003", "Нельзя передать заимствованную (refb) ссылку во владеющий (ref) параметр", value.Expr.Pos);
					return;
				}

				if (value.Expr is StructureFieldAccessExpression)
				{
					AddLifetimeError("LT004", "Нельзя снимать владеющую (ref) ссылку с поля класса", value.Expr.Pos);
					return;
				}

				if (!value.IsAnonymous)
					_lifetimesConsumer.GiveAwayOwnership(value.Expr);

				int lifetimeToGiveAway = _currentBlockNum + 1;
				var variable = objectGetting?.VarData ?? _lifetimeForAssign?.VarData;
				if (linkWithObjectExists && variable != null)
				{
					lifetimeToGiveAway = variable.LifetimeNum;
				}

				GiveAwayOwnership(value, lifetimeToGiveAway, true);
			}
		}

		private void ToDestructPointer(ValueMeta pointer, bool checkAliases)
		{
			if (pointer.VarData == null) throw new NotImplementedException(); // такой хуйни быть не должно

			if (checkAliases && pointer.VarData.Aliases != null && pointer.VarData.Aliases.Count > 0)
			{
				AddLifetimeError("LT010", "Нельзя передавать владение ссылкой, у которой есть алиасы", pointer.Expr.Pos);
				return;
			}

			if (!pointer.VarData.IsInitialized || pointer.VarData.IsDestructed || !pointer.Expr.ResultTypeSpec.IsOwnerRef)
				return;

			var expr = _lifetimesConsumer.PointerLifetimeEnd(pointer.Expr);
			if (expr != null)
			{
				AddToCurrentStatement(expr);
			}
		}

		private void GiveAwayOwnership(ValueMeta pointer, int lifetimeToGiveAway, bool checkAliases)
		{
			if (pointer.VarData == null) return;

			if (checkAliases && pointer.VarData.Aliases != null && pointer.VarData.Aliases.Count > 0)
			{
				AddLifetimeError("LT010", "Нельзя передавать владение ссылкой, у которой есть алиасы", pointer.Expr.Pos);
				return;
			}

			if (CheckLinkedVarsLifetimesError(lifetimeToGiveAway, pointer.VarData))
			{
				AddLifetimeError("LT011", "Попытка передать владение ссылкой, у которой есть связанные ссылки, в область с другим временем жизни", pointer.Expr.Pos);
				return;
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

		private bool CheckLifetimesError(int value, int target)
		{
			if (target == int.MinValue || value == int.MinValue) return true;
			return value > target;
		}

		private bool CheckLinkedVarsLifetimesError(int lifetime, VarMeta varMeta)
		{
			if (varMeta.LinkedVars.Count == 0) return false;
			return varMeta.LifetimeNum != lifetime;
		}

		private void LinkVars(VarMeta var1, VarMeta var2)
		{
			if (var1 == var2) return;

			var1.LinkedVars.Add(var2);
			var2.LinkedVars.Add(var1);

			foreach (var linkedVar in var1.Aliases)
			{
				LinkVars(var2, linkedVar);
			}

			foreach (var linkedVar in var2.Aliases)
			{
				LinkVars(var1, linkedVar);
			}
		}

		private bool CheckLinkWithThis(LifetimeDecl[] lifetimes, string argumentName)
		{
			return lifetimes.Any(x => (x.LeftKey.VarName == argumentName || x.RightKey.VarName == argumentName) &&
									(x.LeftKey.KeyType == LifetimeDecl.KeyType.This || x.RightKey.KeyType == LifetimeDecl.KeyType.This));
		}

		public void ErrorLT002(PositionInSources pos)
		{
			AddLifetimeError("LT002", "Время жизни присваиваемой ссылки меньше времени жизни ссылки-получателя", pos);
		}
	}
}
