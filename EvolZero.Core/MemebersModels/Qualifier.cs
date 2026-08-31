using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public abstract class Qualifier
	{
		public static readonly Qualifier Reference = new ReferenceQualifier();
		public static readonly Qualifier BorrowReference = new BorrowReferenceQualifier();

		public abstract bool Equals(Qualifier other);

		public override bool Equals(object? obj) => obj is Qualifier q && Equals(q);

		public abstract override int GetHashCode();

		public abstract ITypeRef GetTypeRef(IPlatformTypesProvider typesProvider, IEnumerable<Qualifier> nextQualifiers, ITypeRef baseType);
	}

	public sealed class ReferenceQualifier : Qualifier
	{
		public override ITypeRef GetTypeRef(IPlatformTypesProvider typesProvider, IEnumerable<Qualifier> nextQualifiers, ITypeRef baseType)
		{
			return typesProvider.PointerType;
		}

		public override bool Equals(Qualifier other) =>
			other is ReferenceQualifier or BorrowReferenceQualifier;

		public override int GetHashCode() => typeof(ReferenceQualifier).GetHashCode();
	}

	public sealed class ArrayQualifier : Qualifier
	{
		private readonly ulong _size;

		public ArrayQualifier(ulong size)
		{
			_size = size;
		}

		public override ITypeRef GetTypeRef(IPlatformTypesProvider typesProvider, IEnumerable<Qualifier> nextQualifiers, ITypeRef baseType)
		{
			var nextQualifer = nextQualifiers.FirstOrDefault();

			if (nextQualifer != null)
			{
				baseType = nextQualifer.GetTypeRef(typesProvider, nextQualifiers.Skip(1), baseType);
			}

			return typesProvider.MakeArray(baseType, _size);
		}

		public override bool Equals(Qualifier other)
		{
			if (other is not ArrayQualifier arrayQualifier) return false;
			return _size == arrayQualifier._size;
		}

		public override int GetHashCode() => typeof(ArrayQualifier).GetHashCode();
	}

	public sealed class BorrowReferenceQualifier : Qualifier
	{
		public override ITypeRef GetTypeRef(IPlatformTypesProvider typesProvider, IEnumerable<Qualifier> nextQualifiers, ITypeRef baseType)
		{
			return typesProvider.PointerType;
		}

		public override bool Equals(Qualifier other) =>
			other is ReferenceQualifier or BorrowReferenceQualifier;

		public override int GetHashCode() => typeof(ReferenceQualifier).GetHashCode();
	}
}
