using Antlr4.Runtime.Misc;
using EvolZero.Analysis.Semantic;
using EvolZero.Core;
using EvolZero.Core.LogicModels;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using static EvolZero.Core.MemebersModels.Qualifier;

namespace EvolZero.Analysis
{
	internal class SemanticTreeBuilder
	{
		public const string COMPILATION_LAYER = "BasicSemanticsValidator";

		private readonly MembersFinder _membersFinder;
		private readonly TypeAnalyzer _typeAnalyzer;
		private readonly ErrorsBag _errorsBag;

		private Stack<CodeBlock> _blocks = new();

		public PositionInSources CurrentPosition { get; set; }

		public bool UnsafeMode { get; set; } = true;

		private class CodeBlock
		{
			// TODO: здесь наверное сделать параметр показывающий текущий тип блока (функция, класс и тп) чтобы понимать можно ли сюда пихать выражение
			public Statement CurentStatement;
			public List<ILogicModel> StatementChilds;
			public Dictionary<string, Expression> Variables = new();
			public IFunctionalBlockStatement? CurrentFunction;
			public ClassStatement? CurrentClass;

			public CodeBlock(Statement curentStatement, List<ILogicModel> statementChilds, Dictionary<string, Expression> variables, IFunctionalBlockStatement? currentFunction, ClassStatement? currentClass)
			{
				CurentStatement = curentStatement;
				StatementChilds = statementChilds;
				Variables = variables;
				CurrentFunction = currentFunction;
				CurrentClass = currentClass;
			}
		}

		public SemanticTreeBuilder(MembersFinder membersFinder, TypeAnalyzer typeAnalyzer, ErrorsBag errorsBag)
		{
			_membersFinder = membersFinder;
			_typeAnalyzer = typeAnalyzer;
			_errorsBag = errorsBag;
		}

		public void EnterToNameSpace(string nameSpace)
		{
			_membersFinder.AddUsing(nameSpace);
			var childs = new List<ILogicModel>();
			var statement = new NamespaceStatement(nameSpace, childs, CurrentPosition);

			_blocks.Push(new CodeBlock(statement, childs, [], null, null));
		}

		public void Using(string nameSpace)
		{
			if (!_membersFinder.AddUsing(nameSpace))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A namespace was not found", CurrentPosition);
			}
		}

		public void EnterToClass(string className)
		{
			var typeDesc = _membersFinder.FindType(className);
			var childs = new List<ILogicModel>();
			var statement = new ClassStatement(typeDesc, childs, CurrentPosition);

			CodeBlock block = _blocks.Peek();
			block.StatementChilds.Add(statement);

			_blocks.Push(new CodeBlock(statement, childs, [], null, statement));
		}

		public void EnterToFunction(string funcName, List<(TypeSpec Type, string Name)> parameters)
		{
			CodeBlock block = _blocks.Peek();
			var currentClass = block.CurrentClass;

			FuncDesc? funcDesc = null;
			if (currentClass != null)
			{
				var functions = _membersFinder.FindFunction(currentClass.TypeDesc, funcName);
				if (functions == null)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A function with that name was not found", CurrentPosition);
					return;
				}

				funcDesc = _typeAnalyzer.FindSuitableFunction(functions, parameters.Select(x => x.Type), out _);
			}
			else
			{
				var functions = _membersFinder.FindFunction(funcName);
				if (functions == null)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A function with that name was not found", CurrentPosition);
					return;
				}

				funcDesc = _typeAnalyzer.FindSuitableFunction(functions, parameters.Select(x => x.Type), out _);
			}

			if (funcDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Function overload with specified arguments could not be found", CurrentPosition);
				return;
			}

			var childs = new List<ILogicModel>();
			var statement = new FunctionStatement(funcDesc, childs, CurrentPosition);

			block.StatementChilds.Add(statement);

			var variables = new Dictionary<string, Expression>();

			foreach (var param in parameters)
			{
				variables[param.Name] = new VariableAccessExpression(param.Name, param.Type, CurrentPosition);
			}

			_blocks.Push(new CodeBlock(statement, childs, variables, statement, currentClass));
		}

		public void EnterToConstructor(List<(TypeSpec Type, string Name)> parameters)
		{
			CodeBlock block = _blocks.Peek();
			var currentClass = block.CurrentClass;

			if (currentClass == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Declaring a constructor outside of a class is not allowed", CurrentPosition);
				return;
			}

			ConstructorDesc? ctorDesc = null;
			var constructors = _membersFinder.FindConstructors(currentClass.TypeDesc);

			ctorDesc = _typeAnalyzer.FindSuitableConstructor(constructors, parameters.Select(x => x.Type));

			if (ctorDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Constructor with specified arguments could not be found", CurrentPosition);
				return;
			}

			var childs = new List<ILogicModel>();
			var statement = new ConstructorStatement(ctorDesc, childs, new TypeSpec(_membersFinder.FindType("void")), CurrentPosition);

			block.StatementChilds.Add(statement);

			var variables = new Dictionary<string, Expression>();

			foreach (var param in parameters)
			{
				variables[param.Name] = new VariableAccessExpression(param.Name, param.Type, CurrentPosition);
			}

			_blocks.Push(new CodeBlock(statement, childs, variables, statement, currentClass));
		}

		public void EnterToIfBlock(Expression condition)
		{
			var childs = new List<ILogicModel>();
			var statement = new IfStatement(childs, condition, null, null, CurrentPosition);

			CodeBlock block = _blocks.Peek();
			block.StatementChilds.Add(statement);
			var currentFunction = block.CurrentFunction;
			if (currentFunction == null) throw new NotImplementedException();
			var currentClass = block.CurrentClass;
			var variables = new Dictionary<string, Expression>(block.Variables);

			_blocks.Push(new CodeBlock(statement, childs, variables, currentFunction, currentClass));

			if (CheckStubForError(condition)) return;

			if (condition.ResultTypeSpec.Type != TypeNameToTypeDesc("bool"))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The expression passed to 'if' must be of type bool", CurrentPosition);
			}
		}

		public void EnterToWhileBlock(Expression condition)
		{
			var childs = new List<ILogicModel>();
			var statement = new WhileStatement(childs, condition, CurrentPosition);

			CodeBlock block = _blocks.Peek();
			block.StatementChilds.Add(statement);
			var currentFunction = block.CurrentFunction;
			if (currentFunction == null) throw new NotImplementedException();
			var currentClass = block.CurrentClass;
			var variables = new Dictionary<string, Expression>(block.Variables);

			_blocks.Push(new CodeBlock(statement, childs, variables, currentFunction, currentClass));

			if (CheckStubForError(condition)) return;

			if (condition.ResultTypeSpec.Type != TypeNameToTypeDesc("bool"))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The expression passed to 'while' must be of type bool", CurrentPosition);
			}
		}


		public Statement ExitFromBlock()
		{
			var block = _blocks.Pop();
			Statement statement = block.CurentStatement;

			if (statement is IFunctionalBlockStatement fnStatement)
			{
				var voidType = _membersFinder.FindType("void");
				if (fnStatement.ReturnType.Type == voidType)
				{
					block.StatementChilds.Add(new ReturnStatement(new SimpleTypeExpression(new TypeSpec(voidType), CurrentPosition), CurrentPosition));
				}
			}

			return statement;
		}

		public void InserToCurrentBlock(Expression expression)
		{
			_blocks.Peek().StatementChilds.Add(expression);
		}

		public void BuildReturn(Expression returnResult)
		{
			CodeBlock block = _blocks.Peek();
			var currentFunction = block.CurrentFunction;
			if (currentFunction == null) throw new NotImplementedException();

			returnResult = AutoDereferenceIfPointer(returnResult);

			if (!_typeAnalyzer.CheckTypeMatching(currentFunction.ReturnType.Type, returnResult.ResultTypeSpec.Type, out bool needCast)
				|| !currentFunction.ReturnType.QualifiersEquals(returnResult.ResultTypeSpec))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Invalid return type", CurrentPosition);
				return;
			}

			if (needCast && (returnResult.ResultTypeSpec.Type is IntegerTypeDesc or FloatTypeDesc))
			{
				returnResult = ImplicitIntExtenssion(returnResult, currentFunction.ReturnType);
			}

			_blocks.Peek().StatementChilds.Add(new ReturnStatement(returnResult, CurrentPosition));
		}

		public Expression CallFunction(string name, Expression[] args)
		{
			if (CheckStubForError(args)) return new StubForErrorExpression(CurrentPosition);

			var arguments = args.Select(AutoDereferenceIfPointer).ToArray();

			var functions = _membersFinder.FindFunction(name);
			if (functions == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A function with that name was not found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			FuncDesc? funcDesc = _typeAnalyzer.FindSuitableFunction(functions, arguments.Select(x => x.ResultTypeSpec), out TypeSpec?[] casts);

			if (funcDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Function overload with specified arguments could not be found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			for (int i = 0; i < casts.Length; i++)
			{
				TypeSpec? cast = casts[i];
				if (!cast.HasValue || !(arguments[i].ResultTypeSpec.Type is IntegerTypeDesc or FloatTypeDesc)) continue;
				arguments[i] = ImplicitIntExtenssion(arguments[i], cast.Value);
			}

			return new CallFunctionExpression(arguments, funcDesc, CurrentPosition);
		}

		public Expression CallClassMethod(string name, Expression instanceGetting, Expression[] args)
		{
			if (CheckStubForError(args) || CheckStubForError(instanceGetting)) return new StubForErrorExpression(CurrentPosition);

			var arguments = args.Select(AutoDereferenceIfPointer).ToArray();

			// TODO: проверить instanceGetting на валидность
			var func = _membersFinder.FindFunction(instanceGetting.ResultTypeSpec.Type, name);
			if (func == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A method with that name was not found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var funcDesc = _typeAnalyzer.FindSuitableFunction(func, arguments.Select(x => x.ResultTypeSpec), out TypeSpec?[] casts);

			if (funcDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Function overload with specified arguments could not be found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			Expression[] realArgs = new Expression[arguments.Length + 1];
			realArgs[0] = instanceGetting.ResultTypeSpec.IsRef ? instanceGetting : new GetPointerToVarExpression(instanceGetting, CurrentPosition);

			for (int i = 1; i <= arguments.Length; i++)
			{
				var sourceArgumentIndex = i - 1;
				var currentCast = casts[sourceArgumentIndex];
				var currentArg = arguments[sourceArgumentIndex];

				if (currentCast.HasValue && (currentArg.ResultTypeSpec.Type is IntegerTypeDesc))
				{
					realArgs[i] = ImplicitIntExtenssion(currentArg, currentCast.Value);
				}
				else
				{
					realArgs[i] = currentArg;
				}
			}

			return new CallFunctionExpression(realArgs, funcDesc, CurrentPosition);
		}

		public Expression CallHeapConstructor(string typeName, Expression[] args)
		{
			if (CheckStubForError(args)) return new StubForErrorExpression(CurrentPosition);

			var typeDesc = _membersFinder.TryFindType(typeName);
			if (typeDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", $"The type '{typeName}' was not found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var arguments = args.Select(AutoDereferenceIfPointer).ToArray();

			ConstructorDesc? ctorDesc = null;
			var constructors = _membersFinder.FindConstructors(typeDesc);

			ctorDesc = _typeAnalyzer.FindSuitableConstructor(constructors, arguments.Select(x => x.ResultTypeSpec), out TypeSpec?[] casts);

			if (ctorDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Constructor with specified arguments could not be found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			for (int i = 0; i < casts.Length; i++)
			{
				TypeSpec? cast = casts[i];
				if (!cast.HasValue || !(arguments[i].ResultTypeSpec.Type is IntegerTypeDesc or FloatTypeDesc)) continue;
				arguments[i] = ImplicitIntExtenssion(arguments[i], cast.Value);
			}

			var memory = new AllocateHeapMemoryToType(new TypeSpec(typeDesc, [new Qualifier(QKind.Reference)]), pos: CurrentPosition);
			return new CallConstructorExpression(memory, ctorDesc, arguments, CurrentPosition);
		}

		private Expression CallStackConstructor(TypeDesc typeDesc, Expression objMemoryGetting, Expression[]? args)
		{
			if (CheckStubForError(objMemoryGetting)) return new StubForErrorExpression(CurrentPosition);
			if (args != null && CheckStubForError(args)) return new StubForErrorExpression(CurrentPosition);

			ConstructorDesc? ctorDesc = null;
			var constructors = _membersFinder.FindConstructors(typeDesc);

			if (args == null && constructors.Count > 0)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The parameters for the type constructor must be specified explicitly.", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			if (args != null)
			{
				var arguments = args.Select(AutoDereferenceIfPointer).ToArray();
				ctorDesc = _typeAnalyzer.FindSuitableConstructor(constructors, arguments.Select(x => x.ResultTypeSpec), out TypeSpec?[] casts);

				if (ctorDesc == null)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Constructor with specified arguments could not be found", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}

				for (int i = 0; i < casts.Length; i++)
				{
					TypeSpec? cast = casts[i];
					if (!cast.HasValue || !(arguments[i].ResultTypeSpec.Type is IntegerTypeDesc or FloatTypeDesc)) continue;
					arguments[i] = ImplicitIntExtenssion(arguments[i], cast.Value);
				}

				return new CallConstructorExpression(new GetPointerToVarExpression(objMemoryGetting, CurrentPosition), ctorDesc, arguments, CurrentPosition);
			}

			return objMemoryGetting;
		}

		public Expression CreateArrayInHeap(string typeName, Expression arraySize)
		{
			if (CheckStubForError(arraySize)) return new StubForErrorExpression(CurrentPosition);

			var sizeAccessor = AutoDereferenceIfPointer(arraySize);
			if (sizeAccessor.ResultTypeSpec.Type is not IntegerTypeDesc)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The array size must be an integer", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var typeDesc = _membersFinder.TryFindType(typeName);
			if (typeDesc == null)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", $"The type '{typeName}' was not found", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			return new AllocateHeapMemoryToType(new TypeSpec(typeDesc, [new Qualifier(QKind.Reference), new Qualifier(QKind.Array)]), sizeAccessor, CurrentPosition);
		}

		public Expression ClassFieldAccess(Expression instanceGetting, string fieldName)
		{
			if (!instanceGetting.ResultTypeSpec.Type.Variables.TryGetValue(fieldName, out VariableDesc variable))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The class field does not exist", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			return new StructureFieldAccessExpression(variable, instanceGetting.ResultTypeSpec.IsRef, instanceGetting, variable.Declaring, CurrentPosition);
		}

		public ArrayCellAccessExpression ArrayCellAccess(Expression arrayGetting, Expression indexGetting)
		{
			// TODO: сделать проверки на типы
			return new ArrayCellAccessExpression(arrayGetting, indexGetting, CurrentPosition);
		}

		public Expression GetPointerToVar(Expression variable)
		{
			if (CheckStubForError(variable)) return new StubForErrorExpression(CurrentPosition);

			bool isLValue = variable is VariableAccessExpression
				or StructureFieldAccessExpression
				or ArrayCellAccessExpression
				or PointerDereferenceExpression
				or VariableCreatingExpression;

			if (!isLValue)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The 'ref' operator can only be applied to a variable or field", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			return new GetPointerToVarExpression(variable, CurrentPosition);
		}

		public Expression Sum(Expression left, Expression right)
		{
			if (CheckStubForError(left, right)) return new StubForErrorExpression(CurrentPosition);

			var leftAccessor = AutoDereferenceIfPointer(left);
			var rightAccessor = AutoDereferenceIfPointer(right);

			if (leftAccessor.ResultTypeSpec.Type is FloatTypeDesc || rightAccessor.ResultTypeSpec.Type is FloatTypeDesc)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Arithmetic operations with floating-point numbers are not supported yet", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			if (!_typeAnalyzer.SoftCheckTypeMatching(leftAccessor.ResultTypeSpec.Type, rightAccessor.ResultTypeSpec.Type))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The operands of the '+' operation must be of matching types", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var resultType = GetWiderType(leftAccessor.ResultTypeSpec, rightAccessor.ResultTypeSpec);
			leftAccessor = ImplicitIntExtenssion(leftAccessor, resultType);
			rightAccessor = ImplicitIntExtenssion(rightAccessor, resultType);

			return new SimpleBinaryOperationExpression(BinaryOperation.Sum, leftAccessor, rightAccessor, resultType, CurrentPosition);
		}

		public Expression Sub(Expression left, Expression right)
		{
			if (CheckStubForError(left, right)) return new StubForErrorExpression(CurrentPosition);

			var leftAccessor = AutoDereferenceIfPointer(left);
			var rightAccessor = AutoDereferenceIfPointer(right);

			if (leftAccessor.ResultTypeSpec.Type is FloatTypeDesc || rightAccessor.ResultTypeSpec.Type is FloatTypeDesc)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Arithmetic operations with floating-point numbers are not supported yet", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			if (!_typeAnalyzer.SoftCheckTypeMatching(leftAccessor.ResultTypeSpec.Type, rightAccessor.ResultTypeSpec.Type))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The operands of the '-' operation must be of matching types", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var resultType = GetWiderType(leftAccessor.ResultTypeSpec, rightAccessor.ResultTypeSpec);
			leftAccessor = ImplicitIntExtenssion(leftAccessor, resultType);
			rightAccessor = ImplicitIntExtenssion(rightAccessor, resultType);

			return new SimpleBinaryOperationExpression(BinaryOperation.Sub, leftAccessor, rightAccessor, resultType, CurrentPosition);
		}

		public Expression Compare(Expression left, Expression right, CompareOperator compareOperator)
		{
			if (CheckStubForError(left, right)) return new StubForErrorExpression(CurrentPosition);

			var uIntType = _membersFinder.FindType("uint");
			var intType = _membersFinder.FindType("int");

			var leftAccessor = AutoDereferenceIfPointer(left);
			var rightAccessor = AutoDereferenceIfPointer(right);

			var boolTypeSpec = new TypeSpec(TypeNameToTypeDesc("bool"));

			if (leftAccessor.ResultTypeSpec.Type is FloatTypeDesc || rightAccessor.ResultTypeSpec.Type is FloatTypeDesc)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Comparison of floating-point numbers is not supported yet", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			if (_typeAnalyzer.CheckTypeMatching(uIntType, leftAccessor.ResultTypeSpec.Type, out _)
				&& _typeAnalyzer.CheckTypeMatching(uIntType, rightAccessor.ResultTypeSpec.Type, out _))
			{
				//сравнение беззнаковых чисел
				return new CompareOperationExpression(compareOperator, false, leftAccessor, rightAccessor, boolTypeSpec, CurrentPosition);
			}
			else if (_typeAnalyzer.CheckTypeMatching(intType, leftAccessor.ResultTypeSpec.Type, out _)
				&& _typeAnalyzer.CheckTypeMatching(intType, rightAccessor.ResultTypeSpec.Type, out _))
			{
				//сравнение знаковых чисел
				return new CompareOperationExpression(compareOperator, true, leftAccessor, rightAccessor, boolTypeSpec, CurrentPosition);
			}
			else
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The compared operands must be of an integer type", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}
		}

		public Expression LogicalAnd(Expression left, Expression right)
		{
			if (CheckStubForError(left, right)) return new StubForErrorExpression(CurrentPosition);

			var boolType = _membersFinder.FindType("bool");
			if (left.ResultTypeSpec.Type != boolType || right.ResultTypeSpec.Type != boolType)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The operands of '&&' must be of type bool", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var leftAccessor = AutoDereferenceIfPointer(left);
			var rightAccessor = AutoDereferenceIfPointer(right);

			return new SimpleBinaryOperationExpression(BinaryOperation.LogicalAnd, leftAccessor, rightAccessor, new TypeSpec(boolType), CurrentPosition);
		}

		//public Expr LogicalOr(Expr left, Expr right)
		//{
		//	var boolType = _membersFinder.FindType("bool");
		//	if (left.Declaring.Type != boolType || right.Declaring.Type != boolType)
		//		throw new NotImplementedException();

		//	var leftAccessor = AutoDereferenceIfPointer(left);
		//	var rightAccessor = AutoDereferenceIfPointer(right);

		//	throw new NotImplementedException();
		//}

		private Expression BitOperationPrepeare(BinaryOperation operation, Expression left, Expression right)
		{
			if (CheckStubForError(left, right)) return new StubForErrorExpression(CurrentPosition);

			var boolType = _membersFinder.FindType("bool");

			bool isValidType = left.ResultTypeSpec.Type == boolType || left.ResultTypeSpec.Type is IntegerTypeDesc;

			if (left.ResultTypeSpec.Type != right.ResultTypeSpec.Type || !isValidType)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The operands of the bit operation must be of matching integer types or bool", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var leftExpr = AutoDereferenceIfPointer(left);
			var rightExpr = AutoDereferenceIfPointer(right);

			return new SimpleBinaryOperationExpression(operation, leftExpr, rightExpr, new TypeSpec(left.ResultTypeSpec.Type), CurrentPosition);
		}

		public Expression BitAnd(Expression leftExpr, Expression rightExpr)
		{
			return BitOperationPrepeare(BinaryOperation.BitAnd, leftExpr, rightExpr);
		}

		public Expression BitXor(Expression leftExpr, Expression rightExpr)
		{
			return BitOperationPrepeare(BinaryOperation.BitXor, leftExpr, rightExpr);
		}

		public Expression BitOr(Expression leftExpr, Expression rightExpr)
		{
			return BitOperationPrepeare(BinaryOperation.BitOr, leftExpr, rightExpr);
		}

		public Expression BitNot(Expression expr)
		{
			if (CheckStubForError(expr)) return new StubForErrorExpression(CurrentPosition);

			var boolType = _membersFinder.FindType("bool");

			if (expr.ResultTypeSpec.Type != boolType && expr.ResultTypeSpec.Type is not IntegerTypeDesc)
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The '~' operator requires an integer or bool operand", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var accessor = AutoDereferenceIfPointer(expr);

			return new NotExpression(accessor, CurrentPosition);
		}

		public Expression CreateInt(BigInteger num)
		{
			return new NumConstExpression(new TypeSpec(TypeNameToTypeDesc("int")), BaseTypes.Int, num, CurrentPosition);
		}

		public Expression CreateShort(BigInteger num)
		{
			return new NumConstExpression(new TypeSpec(TypeNameToTypeDesc("short")), BaseTypes.Short, num, CurrentPosition);
		}

		public Expression CreateByte(BigInteger num)
		{
			return new NumConstExpression(new TypeSpec(TypeNameToTypeDesc("byte")), BaseTypes.Byte, num, CurrentPosition);
		}

		public Expression CreateString(string str)
		{
			if (str[0] != '"' || str[str.Length - 1] != '"')
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Ivalid string", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			byte[] strBytes = Encoding.UTF8.GetBytes($"{str.Replace(@"\n", Environment.NewLine)[1..^1]}\0");
			return new GlobalArrayExpression(strBytes, new TypeSpec(TypeNameToTypeDesc("byte"), [new Qualifier(QKind.Reference), new Qualifier(QKind.Array)]), CurrentPosition);
		}

		public Expression CreateLocalVariable(string name, TypeSpec declaring, Expression[]? args)
		{
			CodeBlock block = _blocks.Peek();
			if (block.Variables.ContainsKey(name))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "A variable with that name is already declared in this block", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			var varExpr = new VariableCreatingExpression(name, declaring, CurrentPosition);
			block.Variables[name] = new VariableAccessExpression(name, declaring, CurrentPosition);

			if (!declaring.QualifiersExists)
			{
				return CallStackConstructor(declaring.Type, varExpr, args);
			}

			return varExpr;
		}

		public Expression VariableAssing(Expression varExpr, Expression expr, Qualifier? assignQualifier)
		{
			if (CheckStubForError(varExpr, expr)) return new StubForErrorExpression(CurrentPosition);

			if (!_typeAnalyzer.CheckTypeMatching(varExpr.ResultTypeSpec.Type, expr.ResultTypeSpec.Type, out bool needCast))
			{
				_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Cannot assign a value of the specified type", CurrentPosition);
				return new StubForErrorExpression(CurrentPosition);
			}

			if (varExpr.ResultTypeSpec.IsRef && !expr.ResultTypeSpec.IsRef)
			{
				varExpr = new PointerDereferenceExpression(varExpr, CurrentPosition);
			}
			else if (varExpr.ResultTypeSpec.IsRef && expr.ResultTypeSpec.IsRef)
			{
				if (!assignQualifier.HasValue)
				{
					if (!UnsafeMode)
					{
						_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Reassignment of a reference without a 'ref' qualifier requires an unsafe context", CurrentPosition);
						return new StubForErrorExpression(CurrentPosition);
					}

					expr = new PointerDereferenceExpression(expr, CurrentPosition);
					varExpr = new PointerDereferenceExpression(varExpr, CurrentPosition);
				}
				else if (assignQualifier.Value.Kind != QKind.Reference)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "The assignment qualifier must be 'ref'", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}
			}

			if (needCast && (expr.ResultTypeSpec.Type is IntegerTypeDesc or FloatTypeDesc))
			{
				expr = ImplicitIntExtenssion(expr, varExpr.ResultTypeSpec);
			}

			return new SimpleBinaryOperationExpression(BinaryOperation.Assing, varExpr, expr, varExpr.ResultTypeSpec, CurrentPosition);
		}

		public Expression AutoDereferenceIfPointer(Expression expr)
		{
			if (CheckStubForError(expr)) return new StubForErrorExpression(CurrentPosition);

			if (!expr.ResultTypeSpec.IsRef || expr is GetPointerToVarExpression || expr is DoNotAutoDereferenceIfPointerExpression) return expr;
			return new PointerDereferenceExpression(expr, CurrentPosition);
		}

		public Expression VariableAccess(string name)
		{
			CodeBlock block = _blocks.Peek();
			if (!block.Variables.TryGetValue(name, out var value))
			{
				var currentClass = block.CurrentClass?.TypeDesc;
				if (currentClass == null)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "No variable with that name was found", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}

				if (!currentClass.Variables.TryGetValue(name, out var field))
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "No variable or field with that name was found", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}

				TypeSpec fieldDeclaring = field.Declaring;

				var thisGetting = new AppealToThisExpression(currentClass, CurrentPosition);
				return new StructureFieldAccessExpression(field, true, thisGetting, fieldDeclaring, CurrentPosition);
			}

			return value;
		}

		public Expression SetRefQualifier(Expression expr)
		{
			if (CheckStubForError(expr)) return new StubForErrorExpression(CurrentPosition);
			return new DoNotAutoDereferenceIfPointerExpression(expr, CurrentPosition);
		}

		public Expression TypeCast(Expression expr, TypeSpec toType)
		{
			if (CheckStubForError(expr)) return new StubForErrorExpression(CurrentPosition);

			if (!UnsafeMode)
			{
				if (toType.ArrayExists)
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Casting array types is prohibited in a safe context", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}

				if (!toType.QualifiersEquals(expr.ResultTypeSpec) || !_typeAnalyzer.SoftCheckTypeMatching(expr.ResultTypeSpec.Type, toType.Type))
				{
					_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", "Implicit casting of heterogeneous types is prohibited in a safe context", CurrentPosition);
					return new StubForErrorExpression(CurrentPosition);
				}
			}

			return BuildCastExpression(expr, toType);
		}

		private Expression BuildCastExpression(Expression expr, TypeSpec toType)
		{
			TypeSpec fromSpec = expr.ResultTypeSpec;
			TypeDesc fromDesc = fromSpec.Type;
			TypeDesc toDesc = toType.Type;

			bool fromIsPointer = fromSpec.IsRef;
			bool toIsPointer = toType.IsRef;
			bool fromIsInt = fromDesc is IntegerTypeDesc;
			bool toIsInt = toDesc is IntegerTypeDesc;
			bool fromIsFloat = fromDesc is FloatTypeDesc;
			bool toIsFloat = toDesc is FloatTypeDesc;

			if (fromIsPointer && toIsPointer)
				return new CastExpression(expr, toType, CurrentPosition);

			if (fromIsInt && toIsPointer)
				return new IntToPointerExpression(expr, toType, CurrentPosition);

			if (fromIsPointer && toIsInt)
				return new PointerToIntExpression(expr, toType, CurrentPosition);

			if (fromIsInt && toIsFloat)
				return new IntToFloatExtensionExpression(expr, IsSignedInteger(fromSpec), toType, CurrentPosition);

			if (fromIsFloat && toIsInt)
				return new FloatToIntExpression(expr, IsSignedInteger(toType), toType, CurrentPosition);

			if (fromIsInt && toIsInt)
			{
				if (fromDesc == toDesc)
					return new CastExpression(expr, toType, CurrentPosition); // TODO: тут должно быть сообщение что приведение бессмысленно

				if (_typeAnalyzer.CheckTypeMatching(toDesc, fromDesc, out _))
					return new IntToIntExtensionExpression(expr, IsSignedInteger(fromSpec), toType, CurrentPosition);

				if (_typeAnalyzer.CheckTypeMatching(fromDesc, toDesc, out _))
					return new IntTruncExpression(expr, toType, CurrentPosition);

			}

			if (fromIsFloat && toIsFloat)
			{
				if (fromDesc == toDesc)
					return new CastExpression(expr, toType, CurrentPosition);

				if (_typeAnalyzer.CheckTypeMatching(toDesc, fromDesc, out _))
					return new FloatToFloatExpression(expr, toType, CurrentPosition);

				if (_typeAnalyzer.CheckTypeMatching(fromDesc, toDesc, out _))
					return new FloatTruncExpression(expr, toType, CurrentPosition);
			}

			return new ReinterpretCastExpression(expr, toType, CurrentPosition);
		}

		private bool IsSignedInteger(TypeSpec typeSpec)
		{
			var ulongType = _membersFinder.FindType("ulong");
			return !_typeAnalyzer.CheckTypeMatching(typeSpec.Type, ulongType, out _);
		}

		/// <summary>
		/// Создает <see cref="Expression"/> для каста целого числа к переданному <see cref="TypeDesc"/>.
		/// Подразумивается что <paramref name="expr"/> является целым числом, то есть может быть раширен до long, а
		/// <paramref name="resultType"/> либо целоче число, либо число с плвающей точкой.
		/// </summary>
		/// <param name="expr">Выражение, которое нужно привести к типу <paramref name="resultType"/>. 
		/// Должно иметь тип либо целого числа, либо числа с плавающей точкой</param>
		/// <param name="resultType"></param>
		/// <returns></returns>
		private Expression ImplicitIntExtenssion(Expression expr, TypeSpec resultType)
		{
			if (expr.ResultTypeSpec.Type == resultType.Type) return expr;

			var doubleType = _membersFinder.FindType("double");

			if (expr.ResultTypeSpec.Type is FloatTypeDesc && resultType.Type is FloatTypeDesc)
			{
				return new FloatToFloatExpression(expr, resultType, CurrentPosition);
			}

			bool isSigned = IsSignedInteger(expr.ResultTypeSpec);
			bool resultTypeIsFloat = _typeAnalyzer.CheckTypeMatching(resultType.Type, doubleType, out _);

			if (resultTypeIsFloat)
			{
				return new IntToFloatExtensionExpression(expr, isSigned, resultType, CurrentPosition);
			}
			else
			{
				return new IntToIntExtensionExpression(expr, isSigned, resultType, CurrentPosition);
			}
		}

		private TypeSpec GetWiderType(TypeSpec first, TypeSpec second)
		{
			return _typeAnalyzer.CheckTypeMatching(first.Type, second.Type, out _) ? first : second;
		}

		private TypeDesc TypeNameToTypeDesc(string typeName)
		{
			return _membersFinder.FindType(typeName);
		}

		public void ReportError(string message)
		{
			_errorsBag.AddError(COMPILATION_LAYER, "DOLBAEB", message, CurrentPosition);
		}

		private bool CheckStubForError(params Expression[] expressions)
		{
			return expressions.Any(x => x is StubForErrorExpression);
		}

	}
}
