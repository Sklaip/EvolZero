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
		private readonly MembersRefsManager _refsManager;
		private PlatformTypesProvider _typesProvider = null!;

		public Emitter(CodeGenerator codeGenerator, MembersRefsManager refsManager)
		{
			_codeGenerator = codeGenerator;
			_refsManager = refsManager;
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

		private TypeSpec[]? _currentConfiguredGenerics = null;

		protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
		{
			var signatures = _refsManager.GetAllFunctionRefs(statement.BlockDesc);

			foreach (var signature in signatures)
			{
				(TypeSpec? implictArg, IReadOnlyCollection<TypeSpec> args, IFuncRefData refData) = signature;
				if (implictArg != null) _currentConfiguredGenerics = implictArg.Value.ConfiguredGenericParams;

				_functionLocalVariables = new();

				var argumentsTypes = new List<ITypeRef>();
				if (implictArg != null)
					argumentsTypes.Add(GetActualTypeRef(implictArg.Value));

				argumentsTypes.AddRange(args.Select(GetActualTypeRef));

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
				_currentConfiguredGenerics = null;

				_codeGenerator.StopFunctionBodyFill();
			}
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
			var typeSpec = expr.ResultTypeSpec.RemoveFirtsQualifier();
			var type = _refsManager.GetRealTypeRef(typeSpec, _currentConfiguredGenerics) as TypeRef;

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

			var refData = _refsManager.GetFunctionalBlockRef(expr.Function, null);
			return _codeGenerator.FunctionCall(refData, accessors);
		}

		protected override IValueAccessor CallMethod(CallMethodExpression expr)
		{
			var instanceAccessor = HandleExpression(expr.InstanceGetting);
			var argsAccessors = expr.Arguments.Select(HandleExpression);

			var refData = _refsManager.GetFunctionalBlockRef(expr.Method, expr.InstanceGetting.ResultTypeSpec);
			return _codeGenerator.FunctionCall(refData, [instanceAccessor, .. argsAccessors]);
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
			var typeRef = _refsManager.GetTypeRef(expr.ResultTypeSpec, _currentConfiguredGenerics);
			return _codeGenerator.PointerDereference(HandleExpression(expr.Target), typeRef);
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
			var structureType = _refsManager.GetTypeRef(expr.StructureGetting.ResultTypeSpec, _currentConfiguredGenerics);
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

			IValueAccessor firstArgument = memoryGetting;
			if (!expr.ToHeap)
			{
				firstArgument = _codeGenerator.GetPointerToVar(memoryGetting);
			}

			var refData = _refsManager.GetFunctionalBlockRef(expr.Constructor, expr.MemoryGetting.ResultTypeSpec);

			IValueAccessor[] accessors = [firstArgument, .. expr.Arguments.Select(HandleExpression)];
			_codeGenerator.FunctionCall(refData, accessors);

			return memoryGetting;
		}

		protected override IValueAccessor CallDestructor(CallDestructorExpression expr)
		{
			var memoryGetting = HandleExpression(expr.MemoryGetting);
			IValueAccessor[] accessors = [memoryGetting];

			var refData = _refsManager.GetFunctionalBlockRef(expr.Destructor, expr.MemoryGetting.ResultTypeSpec);
			_codeGenerator.FunctionCall(refData, accessors);

			return memoryGetting;
		}

		protected override IValueAccessor CastHandle(CastExpression expr)
		{
			return HandleExpression(expr.Expression);
		}

		protected override IValueAccessor IntToIntExtensionHandle(IntToIntExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);

			var typeRef = _refsManager.GetTypeRef(expr.ResultTypeSpec, _currentConfiguredGenerics);
			return _codeGenerator.IntToIntExtension(accessor, expr.IsSigned, typeRef);
		}

		protected override IValueAccessor IntToFloatExtensionHandle(IntToFloatExtensionExpression expr)
		{
			var accessor = HandleExpression(expr.NumGetting);

			var typeRef = _refsManager.GetTypeRef(expr.ResultTypeSpec, _currentConfiguredGenerics);
			return _codeGenerator.IntToFloatExtension(accessor, expr.IsSigned, typeRef);
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

		protected override IValueAccessor ObjectInitializerHandle(ObjectInitializerExpression expr)
		{
			// TODO: хороший пример кода для compiletime в cevol.
			// Надо придумать как сделать это так, чтобы не нужно было дублировать весь for для того чтобы внутри выбрать
			// GetStackArrayCell или GetStackArrayCell и при том не выносить проверку в цикл, чтобы оно на каждой
			// итерации не проверяло условие, которое можно проверить до входа в цикл. Возможно сделать какой-нибудь делегат времени компиляции
			var objectGetting = HandleExpression(expr.ObjectToInit);
			if (expr.ByRef)
			{
				for (int i = 0; i < expr.Expressions.Count; i++)
				{
					Expression? childExpr = expr.Expressions[i];
					var value = HandleExpression(childExpr);

					var index = _codeGenerator.CreateIntConst((ulong)i, BaseTypes.Int);
					var cellAccessor = _codeGenerator.GetHeapArrayCell(objectGetting, index, GetActualTypeRef(childExpr.ResultTypeSpec));

					_codeGenerator.Assign(cellAccessor, value);
				}
			}
			else
			{
				for (int i = 0; i < expr.Expressions.Count; i++)
				{
					Expression? childExpr = expr.Expressions[i];
					var value = HandleExpression(childExpr);

					var index = _codeGenerator.CreateIntConst((ulong)i, BaseTypes.Int);
					var cellAccessor = _codeGenerator.GetStackArrayCell(objectGetting, index, GetActualTypeRef(childExpr.ResultTypeSpec));

					_codeGenerator.Assign(cellAccessor, value);
				}
			}

			return objectGetting;
		}

		// TODO: а нхуя вообще этот метод нужен?
		private ITypeRef GetActualTypeRef(TypeSpec varDeclaring)
		{
			if (varDeclaring.QualifiersExists)
			{
				return _refsManager.GetRealTypeRef(varDeclaring, _currentConfiguredGenerics);
			}

			return _refsManager.GetTypeRef(varDeclaring, _currentConfiguredGenerics);
		}
	}
}
