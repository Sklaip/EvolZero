using EvolZero.Core;

namespace EvolZero.Generation
{
	internal class PlatformTypesProvider : IPlatformTypesProvider
	{
		private readonly CodeGenerator _codeGenerator;

		public PlatformTypesProvider(CodeGenerator codeGenerator)
		{
			_codeGenerator = codeGenerator;
		}

		public ITypeRef PointerType => _codeGenerator.PointerType;

		public ITypeRef IntType => throw new NotImplementedException();

		public ITypeRef LongType => throw new NotImplementedException();

		public ITypeRef MakeArray(ITypeRef typeRef, ulong size)
		{
			if (typeRef is TypeRef type)
			{
				return _codeGenerator.CreateArrayType(type, size);
			}

			throw new NotImplementedException();
		}
	}
}
