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

			// TODO: тут деструктор в имени не будет иметь окончания, потому что у него нет явных аргументов
			string funcName = blockDesc.Name;
			if (blockDesc.ImplicitArgument != null)
			{
				var arg = blockDesc.ImplicitArgument.Value;
				typesRefs.Add(typeRefGetter(argumentHandler(arg)));
				funcName = $"{blockDesc.ImplicitArgument.Value.Type.Name}_{funcName}";
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

			return _codeGenerator.CreateFunctionSiganture($"{funcName}{genericEndingName}",
				typeRefGetter(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);
		}
	}
}
