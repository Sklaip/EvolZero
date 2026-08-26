using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Generation.Accessors
{
	internal class FuncArgumentAccessor : IValueAccessor
	{
		private readonly LLVMValueRef _func;
		private readonly LLVMTypeRef _type;
		private readonly uint _num;

		public FuncArgumentAccessor(LLVMValueRef func, LLVMTypeRef type, uint num)
		{
			_func = func;
			_type = type;
			_num = num;
		}

		public LLVMValueRef GetRealValue()
		{
			throw new NotImplementedException();
		}

		public LLVMValueRef GetValue()
		{
			return _func.GetParam(_num);
		}

		public void SetValue(LLVMValueRef value)
		{
			throw new NotImplementedException();
		}

		public LLVMTypeRef GetInnerType()
		{
			return _type;
		}
	}
}
