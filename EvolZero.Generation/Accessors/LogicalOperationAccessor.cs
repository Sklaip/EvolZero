using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Generation.Accessors
{
	internal class LogicalOperationAccessor : IValueAccessor
	{
		private readonly Func<LLVMValueRef> _builder;
		private readonly LLVMTypeRef _typeRef;

		public LogicalOperationAccessor(Func<LLVMValueRef> builder, LLVMTypeRef type)
		{
			_builder = builder;
			_typeRef = type;
		}

		public LLVMTypeRef GetInnerType()
		{
			return _typeRef;
		}

		public LLVMValueRef GetRealValue()
		{
			throw new NotImplementedException();
		}

		public LLVMValueRef GetValue()
		{
			return _builder();
		}

		public void SetValue(LLVMValueRef value)
		{
			throw new NotImplementedException();
		}
	}
}
