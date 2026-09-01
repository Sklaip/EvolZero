using EvolZero.Core;
using EvolZero.Generation.Accessors;
using LLVMSharp.Interop;

namespace EvolZero.Generation
{
	public class CodeGenerator
	{
		private readonly LLVMContextRef _context;
		private readonly LLVMModuleRef _module;
		private readonly LLVMBuilderRef _builder;

		//private Dictionary<string, LLVMValueRef> _variablesPointers = new();
		private LLVMValueRef CurrentFunction => _currentFunction.Value;
		private LLVMValueRef? _currentFunction;
		private LLVMValueRef? _funcReturnValuePtr;
		private LLVMBasicBlockRef? _funcReturnBlock;
		private LLVMTypeRef? _funcReturnType;
		private LLVMTypeRef? _retType;

		LLVMTypeRef _mallocType;
		LLVMValueRef _mallocFunc;

		private Stack<LLVMBasicBlockRef> _activeBlocks = new();

		public readonly ITypeRef PointerType;
		public readonly ITypeRef VoidType;

		public CodeGenerator(LLVMContextRef context, LLVMModuleRef module)
		{
			_context = context;
			_module = module;
			_builder = _context.CreateBuilder();

			DeclareMalloc();
			PointerType = new TypeRef(GetPointerType());
			VoidType = new TypeRef(_context.VoidType);
		}

		public ITypeRef GetType(BaseTypes type) => new TypeRef(BaseTypeToLLVMType(type));

		public ITypeRef CreateStructure(string name)
		{
			LLVMTypeRef structureType = _context.CreateNamedStruct(name);
			return new TypeRef(structureType);
		}

		public void FillStructureBody(ITypeRef structure, IEnumerable<ITypeRef> types)
		{
			ToType(structure).StructSetBody(types.Select(ToType).ToArray(), false);
		}

		public IFuncRefData CreateFunctionSiganture(string funcName, ITypeRef resultType, IEnumerable<ITypeRef> argumentsTypes, bool infArgs = false)
		{
			var argumentsTypesArray = argumentsTypes.Select(ToType).ToArray();
			var funcType = LLVMTypeRef.CreateFunction(ToType(resultType), argumentsTypesArray, infArgs);
			var func = _module.AddFunction(funcName, funcType);

			return new FuncRefData
			{
				FuncRef = func,
				TypeRef = funcType,
				ArgumentsTypes = argumentsTypesArray
			};
		}

		public FuncAccessData StartFunctionBodyFill(IFuncRefData funcRefData, string funcName, ITypeRef resultType, IEnumerable<ITypeRef> argumentsTypes)
		{
			var funcData = (FuncRefData)funcRefData;

			// TODO: как-то сделать чтобы инфа уже переданная в метод CreateFunctionSiganture сюда не передавалась, чисто FuncRefData
			_retType = ToType(resultType);
			// TODO: чтобы функцию можно было вызывать из других исполняемых файлов func.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;
			_currentFunction = funcData.FuncRef;
			LLVMBasicBlockRef funcEntry = _context.AppendBasicBlock(funcData.FuncRef, $"{funcName}.entry");
			_funcReturnBlock = _context.AppendBasicBlock(funcData.FuncRef, $"{funcName}.end");
			_builder.PositionAtEnd(funcEntry);

			if (_retType != _context.VoidType)
			{
				_funcReturnValuePtr = _builder.BuildAlloca(ToType(resultType), $"{funcName}.return.value");
			}
			else
			{
				_funcReturnValuePtr = null;
			}

			int count = argumentsTypes.Count();
			var result = new IValueAccessor[count];
			uint i = 0;
			foreach (var arg in argumentsTypes)
			{
				LLVMValueRef ptr = _builder.BuildAlloca(ToType(arg), $"args{i}");

				var accessor = new VarAccessor(_builder, ptr, ToType(arg));
				accessor.SetValue(funcData.FuncRef.GetParam(i));
				result[i] = accessor;
				i++;
			}

			return new FuncAccessData(result, funcData);
		}

		public void AddReturn(IValueAccessor? returnValue)
		{
			if (_funcReturnBlock == null)
				throw new NotImplementedException();

			if (_funcReturnValuePtr != null)
			{
				if (returnValue == null)
					throw new NotImplementedException();
				_builder.BuildStore(returnValue.GetValue(), _funcReturnValuePtr.Value);
			}

			_builder.BuildBr(_funcReturnBlock.Value);
		}

		public void StopFunctionBodyFill()
		{
			if (_funcReturnBlock == null)
				throw new NotImplementedException();

			_builder.PositionAtEnd(_funcReturnBlock.Value);

			if (_retType != null && _funcReturnValuePtr != null)
			{
				var returnValue = _builder.BuildLoad2(_retType.Value, _funcReturnValuePtr.Value);
				_builder.BuildRet(returnValue);
			}
			else
			{
				_builder.BuildRetVoid();
			}

		}

		public IValueAccessor FunctionCall(IFuncRefData funcRefData, IValueAccessor[] valueAccessors)
		{
			var funcDesc = (FuncRefData)funcRefData;

			var args = new LLVMValueRef[valueAccessors.Length];
			for (int i = 0; i < valueAccessors.Length; i++)
			{
				IValueAccessor accessor = valueAccessors[i];
				if (funcDesc.ArgumentsTypes.Length < i)
				{
					accessor = TruncIfInt(funcDesc.ArgumentsTypes[i], accessor);
				}

				args[i] = accessor.GetValue();
			}

			var res = _builder.BuildCall2(funcDesc.TypeRef, funcDesc.FuncRef, args, "");

			return new SimpleValueAccessor(res, funcDesc.TypeRef);
		}

		public FuncAccessData DeclareMalloc()
		{
			_mallocType = LLVMTypeRef.CreateFunction(GetPointerType(), new[] { _context.Int64Type }, false);
			_mallocFunc = _module.AddFunction("malloc", _mallocType);

			return new FuncAccessData(null, new FuncRefData // TODO: че-то с нулом придумать
			{
				FuncRef = _mallocFunc,
				TypeRef = _mallocType
			});
		}

		public FuncAccessData DeclareFree()
		{
			var freeType = LLVMTypeRef.CreateFunction(_context.VoidType, new[] { GetPointerType() }, false);
			var freeFunc = _module.AddFunction("free", freeType);

			return new FuncAccessData(null, new FuncRefData // TODO: че-то с нулом придумать
			{
				FuncRef = freeFunc,
				TypeRef = freeType
			});
		}

		public IValueAccessor GetTypeSize(ITypeRef type)
		{
			var typ = ToType(type);
			return new SimpleValueAccessor(typ.SizeOf, _context.Int64Type);
		}

		public IValueAccessor AllocateHeapMemory(ITypeRef type)
		{
			var memorySize = ToType(type).SizeOf;
			var ptr = _builder.BuildCall2(_mallocType, _mallocFunc, new[] { memorySize }, "malloc");
			return new SimpleValueAccessor(ptr, GetPointerType());
		}

		public IValueAccessor LogicalAnd(IValueAccessor firstOperation, IValueAccessor secondOperation)
		{
			return new LogicalOperationAccessor(() =>
			{
				var func = CurrentFunction;

				var firstResult = firstOperation.GetValue();

				LLVMBasicBlockRef startBlock = _builder.InsertBlock;
				LLVMBasicBlockRef ifBlock = _context.AppendBasicBlock(func, "logicalAnd");
				LLVMBasicBlockRef exitIfBlock = _context.AppendBasicBlock(func, "logicalAnd.Exit");

				_builder.BuildCondBr(firstResult, ifBlock, exitIfBlock);

				_builder.PositionAtEnd(ifBlock);
				var secondResult = secondOperation.GetValue();

				LLVMBasicBlockRef ifBlockEnd = _builder.InsertBlock;
				_builder.BuildBr(exitIfBlock);

				_builder.PositionAtEnd(exitIfBlock);

				LLVMValueRef phiNode = _builder.BuildPhi(_context.Int1Type, "logicalAnd.phi");
				LLVMValueRef constFalse = LLVMValueRef.CreateConstInt(_context.Int1Type, 0);

				phiNode.AddIncoming(new[] { secondResult }, new[] { ifBlockEnd }, 1);
				phiNode.AddIncoming(new[] { constFalse }, new[] { startBlock }, 1);

				return phiNode;
			}, firstOperation.GetInnerType());
		}

		public IValueAccessor BitAnd(IValueAccessor firstOperation, IValueAccessor secondOperation)
		{
			return new LogicalOperationAccessor(() => _builder.BuildAnd(firstOperation.GetValue(), secondOperation.GetValue(), "bit_and"),
				firstOperation.GetInnerType());
		}

		public IValueAccessor BitOr(IValueAccessor firstOperation, IValueAccessor secondOperation)
		{
			return new LogicalOperationAccessor(() => _builder.BuildOr(firstOperation.GetValue(), secondOperation.GetValue(), "bit_or"),
				firstOperation.GetInnerType());
		}

		public IValueAccessor BitXor(IValueAccessor firstOperation, IValueAccessor secondOperation)
		{
			return new LogicalOperationAccessor(() => _builder.BuildXor(firstOperation.GetValue(), secondOperation.GetValue(), "bit_xor"),
				firstOperation.GetInnerType());
		}

		public IValueAccessor BitNot(IValueAccessor operation)
		{
			return new LogicalOperationAccessor(() => _builder.BuildNot(operation.GetValue(), "bit_not"),
				operation.GetInnerType());
		}

		public void CreateIfBlock(IValueAccessor condition, bool elseExists)
		{
			if (_currentFunction == null) throw new NotImplementedException();
			var func = _currentFunction.Value;

			LLVMBasicBlockRef ifBlock = _context.AppendBasicBlock(func, "if.then");
			LLVMBasicBlockRef endIfBlock = _context.AppendBasicBlock(func, "if.merge");

			_activeBlocks.Push(endIfBlock);

			if (elseExists)
			{
				LLVMBasicBlockRef elseBlock = _context.AppendBasicBlock(func, "else.then");

				_builder.BuildCondBr(condition.GetValue(), ifBlock, elseBlock);
				_builder.PositionAtEnd(ifBlock);

				_activeBlocks.Push(elseBlock);
			}
			else
			{
				_builder.BuildCondBr(condition.GetValue(), ifBlock, endIfBlock);
				_builder.PositionAtEnd(ifBlock);
			}
		}

		public void EndIfBlock()
		{
			LLVMBasicBlockRef endIfBlock = _activeBlocks.Pop();
			_builder.BuildBr(endIfBlock);
			_builder.PositionAtEnd(endIfBlock);
		}

		public void CreateElseBlock()
		{
			LLVMBasicBlockRef elseBlock = _activeBlocks.Pop();
			LLVMBasicBlockRef endIfBlock = _activeBlocks.Peek();

			_builder.BuildBr(endIfBlock);
			_builder.PositionAtEnd(elseBlock);
		}

		public void CreateWhileBlock(IValueAccessor condition)
		{
			if (_currentFunction == null) throw new NotImplementedException();
			var func = _currentFunction.Value;

			LLVMBasicBlockRef conditionBlock = _context.AppendBasicBlock(func, "while.condition");
			LLVMBasicBlockRef bodyBlock = _context.AppendBasicBlock(func, "while.body");
			LLVMBasicBlockRef endBlock = _context.AppendBasicBlock(func, "while.merge");

			_builder.BuildBr(conditionBlock);
			_builder.PositionAtEnd(conditionBlock);

			_builder.BuildCondBr(condition.GetValue(), bodyBlock, endBlock);
			_builder.PositionAtEnd(bodyBlock);

			_activeBlocks.Push(endBlock);
			_activeBlocks.Push(conditionBlock);
		}

		public void EndWhileBlock()
		{
			LLVMBasicBlockRef conditionBlock = _activeBlocks.Pop();
			LLVMBasicBlockRef endWhileBlock = _activeBlocks.Pop();

			_builder.BuildBr(conditionBlock);
			_builder.PositionAtEnd(endWhileBlock);
		}

		// TODO: надо сделать оптимизацию чтобы все BuildAlloca вызывались в самом начале метода
		public IValueAccessor CreateVar(string name, ITypeRef type)
		{
			var varType = ToType(type);
			LLVMValueRef ptr = _builder.BuildAlloca(varType, name);

			return new VarAccessor(_builder, ptr, varType);
		}

		public IValueAccessor GetHeapStructureField(IValueAccessor structurePointer, ITypeRef structureType, ITypeRef fieldType, uint fieldNum)
		{
			var ptrToFiled = _builder.BuildStructGEP2(ToType(structureType), structurePointer.GetValue(), fieldNum);
			return new VarAccessor(_builder, ptrToFiled, ToType(fieldType));
		}

		public IValueAccessor GetStackStructureField(IValueAccessor structurePointer, ITypeRef structureType, ITypeRef fieldType, uint fieldNum)
		{
			var ptrToFiled = _builder.BuildStructGEP2(ToType(structureType), structurePointer.GetRealValue(), fieldNum);
			return new VarAccessor(_builder, ptrToFiled, ToType(fieldType));
		}

		public IValueAccessor GetHeapArrayCell(IValueAccessor arrayPointer, IValueAccessor indexGetter, ITypeRef arrayType)
		{
			var elemPtr = _builder.BuildGEP2(ToType(arrayType), arrayPointer.GetValue(), new[] { indexGetter.GetValue() });
			return new VarAccessor(_builder, elemPtr, ToType(arrayType));
		}

		public IValueAccessor GetStackArrayCell(IValueAccessor arrayPointer, IValueAccessor indexGetter, ITypeRef arrayType)
		{
			var elemPtr = _builder.BuildGEP2(ToType(arrayType), arrayPointer.GetRealValue(), new[] { indexGetter.GetValue() });
			return new VarAccessor(_builder, elemPtr, ToType(arrayType));
		}

		public IValueAccessor PointerDereference(IValueAccessor pointer, ITypeRef type)
		{
			//var originalPointer = _builder.BuildLoad2(type.Type, pointer.GetValue());
			//return new VarAccessor(_builder, originalPointer, type.Type);

			return new VarAccessor(_builder, pointer.GetValue(), ToType(type));
		}

		public IValueAccessor GetPointerToVar(IValueAccessor var)
		{
			return new SimpleValueAccessor(var.GetRealValue(), GetPointerType());
		}

		public IValueAccessor CreateIntConst(ulong value, BaseTypes type)
		{
			LLVMTypeRef typeRef;
			switch (type)
			{
				case BaseTypes.Byte:
				case BaseTypes.SByte:
				case BaseTypes.Short:
				case BaseTypes.UShort:
				case BaseTypes.Int:
				case BaseTypes.UInt:
					typeRef = _context.Int32Type;
					break;
				case BaseTypes.ULong:
				case BaseTypes.Long:
					typeRef = _context.Int64Type;
					break;
				default:
					throw new NotImplementedException();
			}

			LLVMValueRef constValue = LLVMValueRef.CreateConstInt(typeRef, value);
			return new SimpleValueAccessor(constValue, typeRef);
		}

		public IValueAccessor CreateGlobalArray(byte[] bytes)
		{
			LLVMTypeRef arrayType = LLVMTypeRef.CreateArray(_context.Int8Type, (uint)bytes.Length);

			LLVMValueRef global = _module.AddGlobal(arrayType, "");

			LLVMValueRef[] values = bytes.Select(b => LLVMValueRef.CreateConstInt(_context.Int8Type, b, false)).ToArray();

			global.Initializer = LLVMValueRef.CreateConstArray(_context.Int8Type, values);

			return new SimpleValueAccessor(global, GetPointerType());
		}

		public TypeRef CreateArrayType(TypeRef type, ulong size)
		{
			LLVMTypeRef arrayType = LLVMTypeRef.CreateArray(type.Type, (uint)size);

			return new TypeRef(arrayType);
		}

		public void Assign(IValueAccessor to, IValueAccessor from)
		{
			var value = TruncIfInt(to, from);
			to.SetValue(value.GetValue());
		}

		public IValueAccessor GetValueByPointer(IValueAccessor ponter, ITypeRef type)
		{
			var originalPointer = ponter.GetValue();
			return new SimpleValueAccessor(_builder.BuildLoad2(ToType(type), originalPointer), ToType(type));
		}

		public IValueAccessor Sum(IValueAccessor a, IValueAccessor b)
		{
			var aValue = a.GetValue();
			var bValue = b.GetValue();

			LLVMTypeRef resultType = a.GetInnerType();
			if (a.GetInnerType().IntWidth < b.GetInnerType().IntWidth)
			{
				aValue = _builder.BuildSExt(aValue, b.GetInnerType(), "sext");
				resultType = b.GetInnerType();
			}
			else if (a.GetInnerType().IntWidth > b.GetInnerType().IntWidth)
			{
				bValue = _builder.BuildSExt(bValue, a.GetInnerType(), "sext");
			}

			LLVMValueRef xNew = _builder.BuildAdd(aValue, bValue);
			return new SimpleValueAccessor(xNew, resultType);
		}

		public IValueAccessor Mul(IValueAccessor a, IValueAccessor b)
		{
			var aValue = a.GetValue();
			var bValue = b.GetValue();

			LLVMTypeRef resultType = a.GetInnerType();
			if (a.GetInnerType().IntWidth < b.GetInnerType().IntWidth)
			{
				aValue = _builder.BuildSExt(aValue, b.GetInnerType(), "sext");
				resultType = b.GetInnerType();
			}
			else if (a.GetInnerType().IntWidth > b.GetInnerType().IntWidth)
			{
				bValue = _builder.BuildSExt(bValue, a.GetInnerType(), "sext");
			}

			LLVMValueRef xNew = _builder.BuildMul(aValue, bValue);
			return new SimpleValueAccessor(xNew, resultType);
		}

		public IValueAccessor Sub(IValueAccessor a, IValueAccessor b)
		{
			LLVMValueRef xNew = _builder.BuildSub(a.GetValue(), b.GetValue());
			return new SimpleValueAccessor(xNew, a.GetInnerType());
		}

		public IValueAccessor Compare(IValueAccessor a, IValueAccessor b, bool signed, CompareOperator compareType)
		{
			LLVMIntPredicate predicate;
			switch (compareType)
			{
				case CompareOperator.Equal:
					predicate = LLVMIntPredicate.LLVMIntEQ;
					break;
				case CompareOperator.GreaterThan:
					predicate = signed ? LLVMIntPredicate.LLVMIntSGT : LLVMIntPredicate.LLVMIntUGT;
					break;
				case CompareOperator.GreaterThanOrEqual:
					predicate = signed ? LLVMIntPredicate.LLVMIntSGE : LLVMIntPredicate.LLVMIntUGE;
					break;
				case CompareOperator.LessThan:
					predicate = signed ? LLVMIntPredicate.LLVMIntSLT : LLVMIntPredicate.LLVMIntULT;
					break;
				case CompareOperator.LessThanOrEqual:
					predicate = signed ? LLVMIntPredicate.LLVMIntSLE : LLVMIntPredicate.LLVMIntULE;
					break;
				default:
					throw new NotImplementedException();
			}

			return new LogicalOperationAccessor(() => _builder.BuildICmp(predicate, a.GetValue(), b.GetValue()), _context.Int1Type);
		}

		public IValueAccessor IntToIntExtension(IValueAccessor value, bool isSigned, ITypeRef type)
		{
			LLVMValueRef res;
			if (isSigned)
			{
				res = _builder.BuildSExt(value.GetValue(), ToType(type));
			}
			else
			{
				res = _builder.BuildZExt(value.GetValue(), ToType(type));
			}

			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor IntToFloatExtension(IValueAccessor value, bool isSigned, ITypeRef type)
		{
			LLVMValueRef res;
			if (isSigned)
			{
				res = _builder.BuildSIToFP(value.GetValue(), ToType(type));
			}
			else
			{
				res = _builder.BuildUIToFP(value.GetValue(), ToType(type));
			}

			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor IntToPointerCast(IValueAccessor value, ITypeRef type)
		{
			var res = _builder.BuildIntToPtr(value.GetValue(), ToType(type));
			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor PointerToIntCast(IValueAccessor value, ITypeRef type)
		{
			var res = _builder.BuildPtrToInt(value.GetValue(), ToType(type));
			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor FloatToIntCast(IValueAccessor value, bool isSigned, ITypeRef type)
		{
			LLVMValueRef res;
			if (isSigned)
			{
				res = _builder.BuildFPToSI(value.GetValue(), ToType(type));
			}
			else
			{
				res = _builder.BuildFPToUI(value.GetValue(), ToType(type));
			}

			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor FloatToFloat(IValueAccessor value, ITypeRef type)
		{
			var res = _builder.BuildFPExt(value.GetValue(), ToType(type));
			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor FloatTruncation(IValueAccessor value, ITypeRef type)
		{
			var res = _builder.BuildFPTrunc(value.GetValue(), ToType(type));
			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor IntTruncation(IValueAccessor value, ITypeRef type)
		{
			var res = _builder.BuildTrunc(value.GetValue(), ToType(type));
			return new SimpleValueAccessor(res, ToType(type));
		}

		public IValueAccessor ReinterpretCast(IValueAccessor value, ITypeRef type)
		{
			if (value is VarAccessor varAccessor)
			{
				return new SimpleValueAccessor(_builder.BuildLoad2(ToType(type), varAccessor.GetRealValue()), ToType(type));
			}

			var ptr = _builder.BuildAlloca(value.GetInnerType());
			_builder.BuildStore(value.GetValue(), ptr);

			return new SimpleValueAccessor(_builder.BuildLoad2(ToType(type), ptr), ToType(type));
		}

		/// <summary>
		/// Если оба параметры числа, то обрезает <paramref name="value"/> до типа <paramref name="dest"/>
		/// Это нужно потому что все числовые константы мы создаем изначально с типом int или long. 
		/// То есть если мы создали константу 10 и хотим записать ее в byte, то нужно сначала это число обрезать, 
		/// ибо мы ему выделили не 1 байт, а 4.
		/// </summary>
		/// <param name="dest">Акссесор на который ориентироваться для усечения <paramref name="dest"/>, из него будет взят только тип</param>
		/// <param name="value">Значение которое возможно будет усечено</param>
		/// <returns>Либо тот же <see cref="IValueAccessor"/> что и был передан, либо <see cref="SimpleValueAccessor"/> с усеченным значением</returns>
		private IValueAccessor TruncIfInt(IValueAccessor dest, IValueAccessor value)
		{
			var destType = dest.GetInnerType();
			return TruncIfInt(destType, value);
		}

		private IValueAccessor TruncIfInt(LLVMTypeRef destType, IValueAccessor value)
		{
			var fromType = value.GetInnerType();

			if ((destType == _context.Int8Type || destType == _context.Int16Type) && fromType == _context.Int32Type)
			{
				LLVMValueRef truncated = _builder.BuildTrunc(value.GetValue(), destType, "narrow");
				return new SimpleValueAccessor(truncated, destType);
			}

			return value;
		}

		private LLVMTypeRef BaseTypeToLLVMType(BaseTypes type)
		{
			switch (type)
			{
				case BaseTypes.Void:
					return _context.VoidType;
				case BaseTypes.Byte:
				case BaseTypes.SByte:
					return _context.Int8Type;
				case BaseTypes.Short:
				case BaseTypes.UShort:
					return _context.Int16Type;
				case BaseTypes.Int:
				case BaseTypes.UInt:
					return _context.Int32Type;
				case BaseTypes.Long:
					return _context.Int64Type;
				case BaseTypes.Float:
					return _context.FloatType;
				case BaseTypes.Double:
					return _context.DoubleType;
				case BaseTypes.Bool:
					return _context.Int1Type;
				case BaseTypes.Pointer:
					return GetPointerType();
				default:
					throw new NotImplementedException();
			}
		}

		private LLVMTypeRef ToType(ITypeRef type)
		{
			return ((TypeRef)type).Type;
		}

		internal LLVMTypeRef GetPointerType()
		{
			return LLVMTypeRef.CreatePointer(_context.Int32Type, 0);
		}

		internal IValueAccessor GetPointerSize()
		{
			return new SimpleValueAccessor(LLVMTypeRef.CreatePointer(_context.Int32Type, 0).SizeOf, _context.Int64Type);
		}

		private LLVMTypeRef[] BaseTypesToLLVMTypes(BaseTypes[] type)
		{
			return type.Select(BaseTypeToLLVMType).ToArray();
		}

		public void VerifyModule()
		{
			_module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
		}

		public LLVMModuleRef GetModule() => _module;
	}
}
