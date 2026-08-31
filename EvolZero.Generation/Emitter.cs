using EvolZero.Core;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation.Accessors;

namespace EvolZero.Generation
{
	public class Emitter
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
			foreach (var child in program.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
		}

		private void HandleStatement(Statement statement)
		{
			switch (statement)
			{
				case NamespaceStatement nameSpaceStatement:
					HandleNameSpace(nameSpaceStatement);
					break;
				case ClassStatement classStatement:
					HandleClass(classStatement);
					break;
				case FunctionStatement functionStatement:
					HandleFunctionalBlock(functionStatement);
					break;
				case ConstructorStatement contructorStatetment:
					HandleFunctionalBlock(contructorStatetment);
					break;
				case IfStatement ifStatement:
					HandleIfStatement(ifStatement);
					break;
				case ReturnStatement returnStatement:
					HandleReturnStatement(returnStatement);
					break;
				case WhileStatement whileStatement:
					HandleWhileStatement(whileStatement);
					break;
				default:
					throw new NotImplementedException();
			}
		}

		private void HandleNameSpace(NamespaceStatement statement)
		{
			foreach (var child in statement.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
		}

		private void HandleClass(ClassStatement statement)
		{
			_currentClass = statement.TypeDesc;
			foreach (var child in statement.Childs)
			{
				if (!(child is Statement stm)) throw new NotImplementedException();
				HandleStatement(stm);
			}
			_currentClass = null;
		}

		private void HandleFunctionalBlock<TBlock>(TBlock statement) where TBlock : Statement, IFunctionalBlockStatement
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
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}

			_functionLocalVariables = null!;

			_codeGenerator.StopFunctionBodyFill();
		}

		private void HandleIfStatement(IfStatement statement)
		{
			var condition = HandleExpression(statement.Condition);
			_codeGenerator.CreateIfBlock(condition);

			foreach (var child in statement.Childs)
			{
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}

			_codeGenerator.EndIfBlock();
		}

		private void HandleReturnStatement(ReturnStatement statement)
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
		}

		private void HandleWhileStatement(WhileStatement statement)
		{
			var condition = HandleExpression(statement.Condition);
			_codeGenerator.CreateWhileBlock(condition);

			foreach (var child in statement.Childs)
			{
				if ((child is Statement stm)) HandleStatement(stm);
				else if (child is Expression expr) HandleExpression(expr);
				else throw new NotImplementedException();
			}

			_codeGenerator.EndWhileBlock();
		}

		private IValueAccessor HandleExpression(Expression expression)
		{
			switch (expression)
			{
				case NumConstExpression numConstExpression:
					return CreateNum(numConstExpression);
				case VariableCreatingExpression varCreate:
					return CreateVar(varCreate);
				case VariableAccessExpression varAccess:
					return VarAccess(varAccess);
				case AllocateHeapMemoryToType allocateHeapMemoryToType:
					return AllocateHeapMemory(allocateHeapMemoryToType);
				case AppealToThisExpression appealToThis:
					return AppealToThis(appealToThis);
				case ArrayCellAccessExpression arrayCellAccess:
					return ArrayCellAccess(arrayCellAccess);
				case SimpleBinaryOperationExpression simpleBinaryOperation:
					return SimpleBinaryOperationHandle(simpleBinaryOperation);
				case CallFunctionExpression callFunction:
					return CallFunction(callFunction);
				case CompareOperationExpression compareOperation:
					return CompareOperationHandle(compareOperation);
				case GetPointerToVarExpression getPointerToVar:
					return GetPointerToVar(getPointerToVar);
				case NotExpression notExpression:
					return Not(notExpression);
				case PointerDereferenceExpression pointerDereference:
					return PointerDereference(pointerDereference);
				case StructureFieldAccessExpression structureFieldAccess:
					return StructureFiledAccess(structureFieldAccess);
				case DoNotAutoDereferenceIfPointerExpression doNotAutoDereferenceIfPointerExpression:
					return HandleExpression(doNotAutoDereferenceIfPointerExpression.Expression);
				case CallConstructorExpression callConstructorExpression:
					return CallConstructor(callConstructorExpression);
				case GlobalArrayExpression globalArrayExpression:
					return CreateGlobalArray(globalArrayExpression);
				case CastExpression castExpression:
					return CastHandle(castExpression);
				case IntToIntExtensionExpression intToIntExtensionExpression:
					return IntToIntExtensionHandle(intToIntExtensionExpression);
				case IntToFloatExtensionExpression intToFloatExtensionExpression:
					return IntToFloatExtensionHandle(intToFloatExtensionExpression);
				case IntTruncExpression intTruncExpression:
					return IntTruncHandle(intTruncExpression);
				case IntToPointerExpression intToPointerExpression:
					return IntToPointerHandle(intToPointerExpression);
				case PointerToIntExpression pointerToIntExpression:
					return PointerToIntHandle(pointerToIntExpression);
				case FloatToIntExpression floatToIntExpression:
					return FloatToIntHandle(floatToIntExpression);
				case FloatToFloatExpression floatToFloatExpression:
					return FloatToFloatHandle(floatToFloatExpression);
				case FloatTruncExpression floatTruncExpression:
					return FloatTruncHandle(floatTruncExpression);
				case ReinterpretCastExpression reinterpretCastExpression:
					return ReinterpretCastHandle(reinterpretCastExpression);
				default:
					throw new NotImplementedException();
			}

		}

		private IValueAccessor CreateNum(NumConstExpression expr)
		{
			var num = (ulong)(expr.Value & ulong.MaxValue);
			return _codeGenerator.CreateIntConst(num, expr.IntType);
		}

		private IValueAccessor CreateVar(VariableCreatingExpression expr)
		{
			var varAccessor = _codeGenerator.CreateVar(expr.Name, GetActualTypeRef(expr.ResultTypeSpec));
			_functionLocalVariables[expr.Name] = varAccessor;

			return varAccessor;
		}

		private IValueAccessor VarAccess(VariableAccessExpression expr)
		{
			return _functionLocalVariables[expr.Name];
		}

		private IValueAccessor AppealToThis(AppealToThisExpression expr)
		{
			if (_currentClass == null || _currentFunctionParentClassRef == null
				|| expr.ResultTypeSpec.Type != _currentClass) throw new NotImplementedException();

			return _currentFunctionParentClassRef;
		}

		private IValueAccessor AllocateHeapMemory(AllocateHeapMemoryToType expr)
		{
			var type = expr.ResultTypeSpec.RemoveFirtsQualifier().GetRealTypeRef(_typesProvider) as TypeRef;

			if (type == null) throw new NotImplementedException();

			return _codeGenerator.AllocateHeapMemory(type);
		}

		private IValueAccessor ArrayCellAccess(ArrayCellAccessExpression expr)
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

		private IValueAccessor SimpleBinaryOperationHandle(SimpleBinaryOperationExpression expr)
		{
			IValueAccessor left = HandleExpression(expr.LeftExpression);
			IValueAccessor right = HandleExpression(expr.RightExpression);

			switch (expr.OperationType)
			{
				case BinaryOperation.Sum:
					return _codeGenerator.Sum(left, right);
				case BinaryOperation.Sub:
					return _codeGenerator.Sub(left, right);
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

		private IValueAccessor CallFunction(CallFunctionExpression expr)
		{
			var accessors = expr.Arguments.Select(HandleExpression).ToArray();
			return _codeGenerator.FunctionCall(expr.Function.RefData, accessors);
		}

		private IValueAccessor CompareOperationHandle(CompareOperationExpression expr)
		{
			var left = HandleExpression(expr.LeftExpression);
			var right = HandleExpression(expr.RightExpression);

			return _codeGenerator.Compare(left, right, expr.IsSigned, expr.CompareOperator);
		}

		private IValueAccessor GetPointerToVar(GetPointerToVarExpression expr)
		{
			return _codeGenerator.GetPointerToVar(HandleExpression(expr.Variable));
		}

		private IValueAccessor Not(NotExpression expr)
		{
			return _codeGenerator.BitNot(HandleExpression(expr));
		}

		private IValueAccessor PointerDereference(PointerDereferenceExpression expr)
		{
			return _codeGenerator.PointerDereference(HandleExpression(expr.Target), expr.ResultTypeSpec.Type.TypeRef);
		}

		private IValueAccessor StructureFiledAccess(StructureFieldAccessExpression expr)
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

		private IValueAccessor CallConstructor(CallConstructorExpression expr)
		{
			var memoryGetting = HandleExpression(expr.MemoryGetting);

			IValueAccessor[] accessors = [memoryGetting, .. expr.Arguments.Select(HandleExpression)];
			_codeGenerator.FunctionCall(expr.Constructor.RefData, accessors);

			return memoryGetting;
		}

		private IValueAccessor CastHandle(CastExpression expr)
		{
			return HandleExpression(expr.Expression);
		}

		private IValueAccessor IntToIntExtensionHandle(IntToIntExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToIntExtension(accessor, expr.IsSigned, expr.ResultTypeSpec.Type.TypeRef);
		}

		private IValueAccessor IntToFloatExtensionHandle(IntToFloatExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToFloatExtension(accessor, expr.IsSigned, expr.ResultTypeSpec.Type.TypeRef);
		}

		private IValueAccessor IntTruncHandle(IntTruncExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntTruncation(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor IntToPointerHandle(IntToPointerExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.IntToPointerCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor PointerToIntHandle(PointerToIntExpression expr)
		{
			var accessor = HandleExpression(expr.PointerGetting);
			return _codeGenerator.PointerToIntCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor FloatToIntHandle(FloatToIntExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatToIntCast(accessor, expr.IsSigned, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor FloatToFloatHandle(FloatToFloatExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatToFloat(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor FloatTruncHandle(FloatTruncExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);
			return _codeGenerator.FloatTruncation(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor ReinterpretCastHandle(ReinterpretCastExpression expr)
		{
			var accessor = HandleExpression(expr.CastedExpression);
			return _codeGenerator.ReinterpretCast(accessor, GetActualTypeRef(expr.ResultTypeSpec));
		}

		private IValueAccessor CreateGlobalArray(GlobalArrayExpression expr)
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
