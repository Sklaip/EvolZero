using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Parsing.Models
{
	public class QualifierWorkpiece
	{
		public string Kind { get; set; }
		public ulong ArraySize { get; set; }

		public QualifierWorkpiece(string kind, ulong arraySize)
		{
			Kind = kind;
			ArraySize = arraySize;
		}

		public QualifierWorkpiece(string kind)
		{
			Kind = kind;
		}

		public Qualifier ToQualifier()
		{
			switch (Kind)
			{
				case "refo": return new OwnerReferenceQualifier();
				case "array": return new ArrayQualifier(ArraySize);
				case "ref": return new BorrowReferenceQualifier();
				case "own": return new OwnQualifier();
				default: throw new NotImplementedException();
			}
		}

		public static Qualifier ToQualifier(QualifierWorkpiece workpiece)
		{
			switch (workpiece.Kind)
			{
				case "refo": return new OwnerReferenceQualifier();
				case "array": return new ArrayQualifier(workpiece.ArraySize);
				case "ref": return new BorrowReferenceQualifier();
				case "own": return new OwnQualifier();
				default: throw new NotImplementedException();
			}
		}

		public static Qualifier[] ToQualifiers(IEnumerable<QualifierWorkpiece> str) => str.Select(ToQualifier).ToArray();
	}
}
