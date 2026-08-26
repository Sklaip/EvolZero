using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Generation.Accessors
{
	internal class VarAccessor : IValueAccessor
	{
		private readonly LLVMBuilderRef _builder;
		private readonly LLVMValueRef _varPtr;
		private readonly LLVMTypeRef _varType;

		public VarAccessor(LLVMBuilderRef builder, LLVMValueRef varPtr, LLVMTypeRef varType)
		{
			_builder = builder;
			_varPtr = varPtr;
			_varType = varType;
		}

		public LLVMTypeRef GetInnerType()
		{
			return _varType;
		}

		public LLVMValueRef GetRealValue()
		{
			//возвращаем ссылку на стек
			return _varPtr;
		}

		public LLVMValueRef GetValue()
		{
			//из стека загружаем значение в регистр и возвращаем этот регистр
			return _builder.BuildLoad2(_varType, _varPtr);
		}

		public void SetValue(LLVMValueRef value)
		{
			_builder.BuildStore(value, _varPtr);
		}
	}
}
