using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Generation.Accessors
{
	internal class SimpleValueAccessor : IValueAccessor
	{
		private readonly LLVMValueRef _constRef;
		private readonly LLVMTypeRef _type;

		public SimpleValueAccessor(LLVMValueRef constRef, LLVMTypeRef type)
		{
			_constRef = constRef;
			_type = type;
		}

		public LLVMTypeRef GetInnerType()
		{
			return _type;
		}

		public LLVMValueRef GetRealValue()
		{
			throw new NotImplementedException();
		}

		public LLVMValueRef GetValue()
		{
			return _constRef;
		}

		public void SetValue(LLVMValueRef value) 
		{
			throw new NotImplementedException();
		}
	}
}
