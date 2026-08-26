using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public readonly record struct Qualifier
	{
		public enum QKind
		{
			Reference,
			Array,
			BorrowReference
		}

		public readonly QKind Kind;

		public Qualifier(QKind kind)
		{
			this.Kind = kind;
		}

		public bool Equals(Qualifier other)
		{
			return other.Kind == Kind
				|| (other.Kind == QKind.Reference && Kind == QKind.BorrowReference)
				|| (other.Kind == QKind.BorrowReference && Kind == QKind.Reference);
		}

		public override int GetHashCode()
		{
			var kind = Kind == QKind.BorrowReference ? QKind.Reference : Kind;
			return kind.GetHashCode();
		}

		public static Qualifier FromString(string str)
		{
			switch (str)
			{
				case "ref": return new Qualifier(QKind.Reference);
				case "array": return new Qualifier(QKind.Array);
				case "refb": return new Qualifier(QKind.BorrowReference);
				default: throw new NotImplementedException();
			}
		}

		public static Qualifier[] FromString(IEnumerable<string> str) => str.Select(FromString).ToArray();
	}
}
