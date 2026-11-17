using EvolZero.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.LSP
{
	internal class TypesProviderStub : IPlatformTypesProvider
	{
		private static readonly TypeRefStub _stub = new TypeRefStub();
		public ITypeRef PointerType => _stub;

		public ITypeRef IntType => _stub;

		public ITypeRef LongType => _stub;

		public ITypeRef MakeArray(ITypeRef typeRef, ulong size) => _stub;
	}
}
