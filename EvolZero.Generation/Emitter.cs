using EvolZero.Core;
using EvolZero.Core.Analysis;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation.Accessors;

namespace EvolZero.Generation
{
	public class Emitter : SemanticTreeVisitor<IValueAccessor>
	{
		private CodeGenerator _codeGenerator = null!;
		private PlatformTypesProvider _typesProvider = null!;

		public Emitter(CodeGenerator codeGenerator)
		{
			_codeGenerator = codeGenerator;
			_typesProvider = new PlatformTypesProvider(codeGenerator);
		}

		private TypeDesc? _currentClass = null;
		private Dictionary<string, IValueAccessor> _functionLocalVariables = null!;
		private IValueAccessor? _currentFunctionParentClassRef = null;

		public CodeGenerator CodeGenerator { get => _codeGenerator; }

		public void Build(ProgramStatement program)
		{
			Visit(program);
		}

		protected override void SubTreeEnd(IValueAccessor? value)
		{
		}

		protected override void HandleNameSpace(NamespaceStatement statement)
		{
			foreach (var child in statement.Childs)
			{
				if (child is Statement stm) HandleStatement(stm);
				else throw new NotImplementedException();
			}
		}

		protected override void HandleClass(ClassStatement statement)
		{
			_currentClass = statement.TypeDesc;
			foreach (var child in statement.Childs)
			{
				if (child is Statement stm) HandleStatement(stm);
				else throw new NotImplementedException();
			}
			_currentClass = null;
		}

		protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
		{
			_functionLocalVariables = new();

			var argumentsTypes = new List<ITypeRef>();
			if (_currentClass != null) argumentsTypes.Add(_codeGenerator.PointerType);
			argumentsTypes.AddRange(statement.Arguments.Select(x => GetActualTypeRef(x.Declaring)));

			IFuncRefData refData = statement.RefData;
			string name = statement.Name;
			ITypeRef returnType = GetActualTypeRef(statement.ReturnType);

			var funcData = _codeGenerator.StartFunctionBodyFill(refData, name, returnType, argumentsTypes);

			if (_currentClass != null)
			{
				_currentFunctionParentClassRef = funcData.Arguments[0];

				for (int i = 1; i < funcData.Arguments.Length; i++)
				{
					IValueAccessor? accessor = funcData.Arguments[i];
					var arg = statement.Arguments[i - 1];
					_functionLocalVariables.Add(arg.Name, accessor);
				}
			}
			else
			{
				for (int i = 0; i < funcData.Arguments.Length; i++)
				{
					IValueAccessor? accessor = funcData.Arguments[i];
					var arg = statement.Arguments[i];
					_functionLocalVariables.Add(arg.Name, accessor);
				}
			}

			foreach (var child in statement.Childs)
			{
				if (child is Statement stm) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}

			_functionLocalVariables = null!;

			_codeGenerator.StopFunctionBodyFill();
		}

		protected override void HandleIfStatement(IfStatement statement)
		{
			var condition = HandleExpression(statement.Condition);

			if (statement.ElseIfStatements.Count == 0 && statement.ElseStatement == null)
			{
				_codeGenerator.CreateIfBlock(condition, false);
				HandleStatemetChilds(statement);
				_codeGenerator.EndIfBlock();
			}
			else if (statement.ElseIfStatements.Count == 0 && statement.ElseStatement != null)
			{
				_codeGenerator.CreateIfBlock(condition, true);
				HandleStatemetChilds(statement);

				_codeGenerator.CreateElseBlock();
				HandleStatemetChilds(statement.ElseStatement);

				_codeGenerator.EndIfBlock();
			}
			else
			{
				_codeGenerator.CreateIfBlock(condition, true);
				HandleStatemetChilds(statement);

				_codeGenerator.CreateElseBlock();
				HandleIfStatement(statement.Decompose());

				_codeGenerator.EndIfBlock();
			}
		}

		protected override void HandleStatemetChilds(Statement statement)
		{
			foreach (var child in statement.Childs)
			{
				if (child is Statement stm) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}
		}

		protected override IValueAccessor? HandleReturnStatement(ReturnStatement statement)
		{
			Expression resExpr = statement.Value;
			if (resExpr.ResultTypeSpec.Type.Name != "void")
			{
				_codeGenerator.AddReturn(HandleExpression(resExpr));
			}
			else
			{
				_codeGenerator.AddReturn(null);
			}

			return default;
		}

		protected override void HandleWhileStatement(WhileStatement statement)
		{
			var condition = HandleExpression(statement.Condition);
			_codeGenerator.CreateWhileBlock(condition);

			foreach (var child in statement.Childs)
			{
				if (child is Statement stm) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}

			_codeGenerator.EndWhileBlock();
		}

		protected override IValueAccessor CreateNum(NumConstExpression expr)
		{
			var num = (ulong)(expr.Value & ulong.MaxValue);
			return _codeGenerator.CreateIntConst(num, expr.IntType);
		}

		protected override IValueAccessor CreateVar(VariableCreatingExpression expr)
		{
			var varAccessor = _codeGenerator.CreateVar(expr.Name, GetActualTypeRef(expr.ResultTypeSpec));
			_functionLocalVariables[expr.Name] = varAccessor;

			return varAccessor;
		}

		protected override IValueAccessor VarAccess(VariableAccessExpression expr)
		{
			return _functionLocalVariables[expr.Name];
		}

		protected override IValueAccessor AppealToThis(AppealToThisExpression expr)
		{
			if (_currentClass == null || _currentFunctionParentClassRef == null
				|| expr.ResultTypeSpec.Type != _currentClass) throw new NotImplementedException();

			return _currentFunctionParentClassRef;
		}

		protected override IValueAccessor AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			var type = expr.ResultTypeSpec.RemoveFirtsQualifier().GetRealTypeRef(_typesProvider) as TypeRef;

			if (type == null) throw new NotImplementedException();

			return _codeGenerator.AllocateHeapMemory(type);
		}

		protected override IValueAccessor FreeHeapMemory(DestructPointerExpression expr)
		{
			var ptr = HandleExpression(expr.Expr);
			return _codeGenerator.FreeHeapMemory(ptr);
		}

		protected override IValueAccessor ArrayCellAccess(ArrayCellAccessExpression expr)
		{
			bool byRef = false;

			var arrayGetting = expr.ArrayGetting;
			if (arrayGetting is PointerDereferenceExpression pointerDereference)
			{
				byRef = true;
				arrayGetting = pointerDereference.Target;
			}

			var arrayAccessor = HandleExpression(arrayGetting);
			var indexAccessor = HandleExpression(expr.IndexGetting);

			if (byRef)
			{
				return _codeGenerator.GetHeapArrayCell(arrayAccessor, indexAccessor, GetActualTypeRef(expr.ResultTypeSpec));
			}
			else
			{
				return _codeGenerator.GetStackArrayCell(arrayAccessor, indexAccessor, GetActualTypeRef(expr.ResultTypeSpec));
			}
		}

		protected override IValueAccessor SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
		{
			IValueAccessor left = HandleExpression(expr.LeftExpression);
			IValueAccessor right = HandleExpression(expr.RightExpression);

			switch (expr.OperationType)
			{
				case BinaryOperation.Sum:
					return _codeGenerator.Sum(left, right);
				case BinaryOperation.Sub:
					return _codeGenerator.Sub(left, right);
				case BinaryOperation.Multiple:
					return _codeGenerator.Mul(left, right);
				case BinaryOperation.Assing:
					_codeGenerator.Assign(left, right);
					return right;
				case BinaryOperation.BitAnd:
					return _codeGenerator.BitAnd(left, right);
				case BinaryOperation.BitOr:
					return _codeGenerator.BitOr(left, right);
				case BinaryOperation.BitXor:
					return _codeGenerator.BitXor(left, right);
				case BinaryOperation.LogicalAnd:
					return _codeGenerator.LogicalAnd(left, right);
				default:
					throw new NotImplementedException();
			}
		}

		protected override IValueAccessor Exchange(ExchangeExpression expr)
		{
			var target = HandleExpression(expr.Target);
			var value = HandleExpression(expr.Value);

			return _codeGenerator.Exchange(target, value);
		}

		protected override IValueAccessor CallFunction(CallFunctionExpression expr)
		{
			var accessors = expr.Arguments.Select(HandleExpression).ToArray();
			return _codeGenerator.FunctionCall(expr.Function.RefData, accessors);
		}

		protected override IValueAccessor CompareOperationHandle(CompareOperationExpression expr)
		{
			var left = HandleExpression(expr.LeftExpression);
			var right = HandleExpression(expr.RightExpression);

			return _codeGenerator.Compare(left, right, expr.IsSigned, expr.CompareOperator);
		}

		protected override IValueAccessor GetPointerToVar(GetPointerToVarExpression expr)
		{
			return _codeGenerator.GetPointerToVar(HandleExpression(expr.Variable));
		}

		protected override IValueAccessor Not(NotExpression expr)
		{
			return _codeGenerator.BitNot(HandleExpression(expr.Expression));
		}

		protected override IValueAccessor PointerDereference(PointerDereferenceExpression expr)
		{
			return _codeGenerator.PointerDereference(HandleExpression(expr.Target), expr.ResultTypeSpec.Type.TypeRef);
		}

		protected override IValueAccessor StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			bool byRef = false;

			var structureGetting = expr.StructureGetting;
			if (structureGetting is PointerDereferenceExpression pointerDereference)
			{
				byRef = true;
				structureGetting = pointerDereference.Target;
			}

			var instanceGetting = HandleExpression(structureGetting);
			var structureType = expr.StructureGetting.ResultTypeSpec.Type.TypeRef;
			var fieldTypeRef = GetActualTypeRef(expr.ResultTypeSpec);

			if (byRef)
			{
				return _codeGenerator.GetHeapStructureField(instanceGetting, structureType, fieldTypeRef, expr.FiledNum);
			}
			else
			{
				return _codeGenerator.GetStackStructureField(instanceGetting, structureType, fieldTypeRef, expr.FiledNum);
			}
		}

		protected override IValueAccessor CallConstructor(CallConstructorExpression expr)
		{
			var memoryGetting = HandleExpression(expr.MemoryGetting);

			IValueAccessor[] accessors = [memoryGetting, .. expr.Arguments.Select(HandleExpression)];
			_codeGenerator.FunctionCall(expr.Constructor.RefData, accessors);

			return memoryGetting;
		}

		protected override IValueAccessor CallDestructor(CallDesructorExpression expr)
		{
			var memoryGetting = HandleExpression(expr.MemoryGetting);
			IValueAccessor[] accessors = [memoryGetting];
			_codeGenerator.FunctionCall(expr.Destructor.RefData, accessors);

			return memoryGetting;
		}

		protected override IValueAccessor CastHandle(CastExpression expr)
		{
			return HandleExpression(expr.Expression);
		}

		protected override IValueAccessor IntToIntExtensionHandle(IntToIntExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToIntExtension(accessor, expr.IsSigned, expr.ResultTypeSpec.Type.TypeRef);
		}

		protected override IValueAccessor IntToFloatExtensionHandle(IntToFloatExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToFloatExtension(accessor, expr.IsSigned, expr.ResultTypeSpec.Type.TypeRef);
		}

		protected override IValueAccessor IntTruncHandle(IntTruncExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntTruncation(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor IntToPointerHandle(IntToPointerExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToPointerCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor PointerToIntHandle(PointerToIntExpression expr)
		{
			var accessor = HandleExpression(expr.PointerGetting);
			return _codeGenerator.PointerToIntCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor FloatToIntHandle(FloatToIntExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatToIntCast(accessor, expr.IsSigned, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor FloatToFloatHandle(FloatToFloatExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatToFloat(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor FloatTruncHandle(FloatTruncExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatTruncation(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor ReinterpretCastHandle(ReinterpretCastExpression expr)
		{
			var accessor = HandleExpression(expr.CastedExpression);
			return _codeGenerator.ReinterpretCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		protected override IValueAccessor CreateGlobalArray(GlobalArrayExpression expr)
		{
			return _codeGenerator.CreateGlobalArray(expr.Array);
		}

		private ITypeRef GetActualTypeRef(TypeSpec varDeclaring)
		{
			if (varDeclaring.QualifiersExists) return varDeclaring.GetRealTypeRef(_typesProvider);
			return varDeclaring.Type.TypeRef;
		}
	}
}
