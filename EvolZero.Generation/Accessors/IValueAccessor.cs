using LLVMSharp.Interop;

namespace EvolZero.Generation.Accessors
{
	public interface IValueAccessor
	{
		LLVMValueRef GetValue();
		LLVMValueRef GetRealValue();
		void SetValue(LLVMValueRef value);
		LLVMTypeRef GetInnerType();
	}
}
