using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Parsing.Models
{
	internal class QualifierWorkpiece
	{
		public string Kind { get; set; }
		public ulong ArraySize { get; set; }

		public Qualifier ToQualifier()
		{
			switch (Kind)
			{
				case "ref": return new ReferenceQualifier();
				case "array": return new ArrayQualifier(ArraySize);
				case "refb": return new BorrowReferenceQualifier();
				default: throw new NotImplementedException();
			}
		}

		public static Qualifier ToQualifier(QualifierWorkpiece workpiece)
		{
			switch (workpiece.Kind)
			{
				case "ref": return new ReferenceQualifier();
				case "array": return new ArrayQualifier(workpiece.ArraySize);
				case "refb": return new BorrowReferenceQualifier();
				default: throw new NotImplementedException();
			}
		}

		public static Qualifier[] ToQualifiers(IEnumerable<QualifierWorkpiece> str) => str.Select(ToQualifier).ToArray();
	}
}
