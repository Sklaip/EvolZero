using System;
using System.Collections.Generic;
using System.Text;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.Analysis.Semantic
{
	public class LifeTime
	{
		public Expression Expr { get; set; }
		public int BlockNum { get; set; }
		public bool IsInitialized { get; set; }
		public bool ToLocalValue { get; set; }
		public bool IsNotRef { get; set; }
		public bool IsAnonymous { get; set; }
		public List<LifeTime> ActiveAliases { get; set; }
		public LifeTime? IsAliaseTo { get; set; }
	}

	public class LifeTimesAnalyzer : SemanticTreeVisitor<LifeTime?>
	{
		private int _currentBlockNum = -1; // -1 чтобы был 0, потому что при первом входе в HandleStatemetChilds будет инкремент
		private Dictionary<string, int> _variablesBlocks = new();
		private HashSet<string> _givenRefs = new();
		private Stack<List<LifeTime>> _currentLifetimes = new();
		private readonly ErrorsBag _errorsBag;

		public LifeTimesAnalyzer(ErrorsBag errorsBag)
		{
			_errorsBag = errorsBag;
		}

		protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
		{
			base.HandleFunctionalBlock(statement);

			_givenRefs = new();
			_currentLifetimes = new();
			_variablesBlocks = new();
			_currentBlockNum = -1;
		}

		protected override void HandleStatemetChilds(Statement statement)
		{
			_currentLifetimes.Push(new());
			_currentBlockNum++;

			base.HandleStatemetChilds(statement);

			_currentBlockNum--;
			ExitFromBlock();
		}

		protected override void SubTreeEnd(LifeTime? value)
		{
			if (value == null) return;

			if (value.IsAnonymous)
			{
				ToDestructPointer(value, false);
			}
		}

		protected override LifeTime AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsInitialized = true,
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
				IsInitialized = true,
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
				IsInitialized = true,
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
				IsInitialized = true,
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime CreateGlobalArray(GlobalArrayExpression expr)
		{
			// TODO: вроде array называется global, а вроде мы его потом удалять будем. Хуета какая-то
			return new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsInitialized = true,
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime? StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			var structureGetting = HandleExpression(expr.StructureGetting);

			if (!expr.ResultTypeSpec.IsRef)
			{
				return new LifeTime()
				{
					Expr = expr,
					BlockNum = structureGetting.BlockNum,
					IsInitialized = true,
					IsNotRef = true
				};
			}

			if (structureGetting == null) throw new NotImplementedException(); //для дебага. Такого быть не должно

			return new LifeTime()
			{
				Expr = expr,
				BlockNum = structureGetting.BlockNum,
				IsInitialized = true,
				ToLocalValue = false
			};
		}

		protected override LifeTime? VarAccess(VariableAccessExpression expr)
		{
			int blocknum = 0;
			_variablesBlocks.TryGetValue(expr.Name, out blocknum);

			if (!expr.ResultTypeSpec.IsRef)
			{
				return new LifeTime()
				{
					Expr = expr,
					BlockNum = blocknum,
					IsInitialized = true,
					IsNotRef = true
				};
			}


			if (_givenRefs.Contains(expr.Name))
			{
				throw new NotImplementedException(); // тут ошибка что нельзя обратиться к ссылке которую мы отдали
			}

			return new LifeTime()
			{
				Expr = expr,
				BlockNum = blocknum,
				IsInitialized = true,
				ToLocalValue = false
			};
		}

		protected override LifeTime? CallFunction(CallFunctionExpression expr)
		{
			int i = 0;
			int j = 0;
			if (expr.Function.DeclaringType != null) j++;

			var acceptedArguments = expr.Function.Arguments;
			var passedArguments = expr.Arguments;

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
				IsInitialized = true,
				ToLocalValue = false,
				IsAnonymous = true
			};
		}

		protected override LifeTime? CreateVar(VariableCreatingExpression expr)
		{
			_variablesBlocks[expr.Name] = _currentBlockNum;
			if (!expr.ResultTypeSpec.IsRef) return null;

			var lifetime = new LifeTime()
			{
				Expr = expr,
				BlockNum = _currentBlockNum,
				IsInitialized = false,
				ToLocalValue = false
			};

			_currentLifetimes.Peek().Add(lifetime);

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
					return right;
				default:
					return null;
			}
		}

		private void AssingHandler(LifeTime variable, LifeTime value)
		{
			if (!variable.Expr.ResultTypeSpec.IsRef || !value.Expr.ResultTypeSpec.IsRef) return;

			if (value.BlockNum > variable.BlockNum && !value.IsAnonymous)
				throw new NotImplementedException(); // ошибка что время жизни больше времени жизни ссылки

			if (!value.IsInitialized)
				throw new NotImplementedException(); // ссылка был деинициализированна. Вообще сюда оно попадть не должно, оно должно отбрасываться на других проверках

			var varIsOwner = variable.Expr.ResultTypeSpec.IsOwnerRef;

			if (varIsOwner)
			{
				if (!value.Expr.ResultTypeSpec.IsOwnerRef)
					throw new NotImplementedException(); // ошибка что во владеющую ссылку нельзя пихать заимствованные значения

				if (value.Expr is StructureFieldAccessExpression)
					throw new NotImplementedException(); // нельзя снимать владеюущие ссылки с классов. Потом для этого сделать оператор замены или ссылку обнулять

				if (variable.IsInitialized && !variable.ToLocalValue)
					ToDestructPointer(variable, false);

				variable.BlockNum = value.BlockNum;

				GiveAwayOwnership(value);
			}
			else
			{
				if (value.IsAnonymous && !value.ToLocalValue)
					throw new NotImplementedException(); // ошибка что анонимные ссылоки (напрмиер те что выдаются через new и loc) можно присвивать только во владеющие ссылки

				var parent = value.IsAliaseTo ?? value;
				variable.IsAliaseTo = parent;

				parent.ActiveAliases ??= new List<LifeTime>();
				parent.ActiveAliases.Add(variable);
			}

			variable.IsInitialized = true;
			variable.ToLocalValue = value.ToLocalValue;
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

		private void ToDestructPointer(LifeTime pointer, bool isEndBlock)
		{
			//если у ссылки есть алиасы, то удаляем ее в конце блока, если нету, то немедленно
		}

		private void GiveAwayOwnership(LifeTime pointer)
		{
			if (pointer.ActiveAliases != null && pointer.ActiveAliases.Count > 0)
			{
				throw new NotImplementedException(); // ошибка что нельзя передавать владение ссылкой у которой есть алиасы
			}

			if (pointer.Expr is VariableAccessExpression accessExpression)
			{
				_givenRefs.Add(accessExpression.Name);
				pointer.IsInitialized = false;
			}
		}

		private void ExitFromBlock()
		{
			foreach (var lifetime in _currentLifetimes.Pop())
			{
				ToDestructPointer(lifetime, true);
			}
		}

		/* Правила работы:
		Если отдали владение ссылки, то дальнейшее ее использование запрещается

		 */
	}
}
