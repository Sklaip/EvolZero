using EvolZero.Generation;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

namespace EvolZero.Core.MemebersModels
{
	public readonly struct TypeSpec : IEquatable<TypeSpec>
	{
		public readonly TypeDesc Type;
		public readonly Qualifier[] Qualifiers;

		public bool QualifiersExists => Qualifiers != null && Qualifiers.Length > 0;
		public bool IsRef => QualifiersExists && (Qualifiers[0].Kind == Qualifier.QKind.Reference || Qualifiers[0].Kind == Qualifier.QKind.BorrowReference);
		public bool IsBorrowRef => QualifiersExists && Qualifiers[0].Kind == Qualifier.QKind.BorrowReference;
		public bool IsOwnerRef => QualifiersExists && Qualifiers[0].Kind == Qualifier.QKind.Reference;
		public bool ArrayExists => Qualifiers.Any(x => x.Kind == Qualifier.QKind.Array);

		public TypeSpec()
		{
			throw new NotImplementedException();
		}

		public TypeSpec(TypeDesc type)
		{
			Type = type;
			Qualifiers = Array.Empty<Qualifier>();
		}

		public TypeSpec(TypeDesc type, Qualifier[] qualifiers)
		{
			Type = type;
			Qualifiers = qualifiers;
		}

		public bool QualifiersEquals(TypeSpec other)
		{
			if (other.Qualifiers == null || Qualifiers == null) return false;
			if (Qualifiers.Length != other.Qualifiers.Length) return false;

			for (int i = 0; i < Qualifiers.Length; i++)
			{
				Qualifier qualifier = Qualifiers[i];

				if (!qualifier.Equals(other.Qualifiers[i])) return false;
			}

			return true;
		}

		public bool Equals(TypeSpec other)
		{
			return Type == other.Type && QualifiersEquals(other);
		}

		public override bool Equals(object obj)
		{
			return obj is TypeSpec && Equals((TypeSpec)obj);
		}

		public override int GetHashCode()
		{
			var hashCode = new HashCode();
			hashCode.Add(Type);

			if (Qualifiers != null)
			{
				foreach (var t in Qualifiers)
				{
					hashCode.Add(t);
				}
			}

			return hashCode.ToHashCode();
		}
	}
}
