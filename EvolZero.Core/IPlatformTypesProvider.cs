using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core
{
	public interface IPlatformTypesProvider
	{
		ITypeRef PointerType { get; }
		ITypeRef IntType { get; }
		ITypeRef LongType { get; }
		ITypeRef MakeArray(ITypeRef typeRef, ulong size);
	}
}
