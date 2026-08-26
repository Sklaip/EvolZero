using EvolZero.Core;
using LLVMSharp.Interop;

namespace EvolZero.Generation
{
	public class FuncRefData : IFuncRefData
	{
		public LLVMTypeRef TypeRef { get; set; }
		public LLVMValueRef FuncRef { get; set; }
		public LLVMTypeRef[] ArgumentsTypes { get; set; }
	}
}
