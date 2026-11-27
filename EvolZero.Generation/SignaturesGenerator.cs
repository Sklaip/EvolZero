using EvolZero.Core;
using EvolZero.Core.MemebersModels;

namespace EvolZero.Generation
{
	public class SignaturesGenerator : ISignaturesGenerator
	{
		private readonly ICodeGenerator _codeGenerator;

		public SignaturesGenerator(ICodeGenerator codeGenerator)

		{
			_codeGenerator = codeGenerator;
		}

		public IFuncRefData CreateFunctionSiganture(IFunctionalBlockDesc blockDesc,
			Func<TypeSpec, ITypeRef> typeRefGetter, Func<TypeSpec, TypeSpec> argumentHandler)
		{
			var typesRefs = new List<ITypeRef>();

			if (blockDesc.ImplicitArgument != null)
			{
				var arg = blockDesc.ImplicitArgument.Value;
				typesRefs.Add(typeRefGetter(argumentHandler(arg)));
			}

			string genericEndingName = string.Empty;

			var arguments = blockDesc.Arguments;
			for (int i = 0; i < arguments.Length; i++)
			{
				var typeSpec = arguments[i].Declaring;
				var actualTypeSpec = argumentHandler(typeSpec);

				if (typeSpec.Type is GenericParamDesc)
				{
					genericEndingName = $"{genericEndingName}__gp{i}{actualTypeSpec.ToString("_")}";
				}

				typesRefs.Add(typeRefGetter(actualTypeSpec));
			}

			return _codeGenerator.CreateFunctionSiganture($"{blockDesc.Name}{genericEndingName}",
				typeRefGetter(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);
		}
	}
}
