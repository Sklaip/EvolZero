using EvolZero.Core;
using LLVMSharp.Interop;

namespace EvolZero.Generation
{
	public record TypeRef(LLVMTypeRef Type, BaseTypes? baseType = null, bool isPtr = false) : ITypeRef;
}
