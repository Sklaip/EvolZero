using EvolZero.Core;
using EvolZero.Core.MemebersModels;

namespace EvolZero.Core
{
	public interface ISignaturesGenerator
	{
		IFuncRefData CreateFunctionSiganture(IFunctionalBlockDesc blockDesc, Func<TypeSpec, ITypeRef> typeRefGetter, Func<TypeSpec, TypeSpec> argumentHandler);
	}
}