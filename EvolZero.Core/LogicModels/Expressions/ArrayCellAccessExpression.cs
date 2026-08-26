using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class ArrayCellAccessExpression : Expression
	{
		public readonly Expression ArrayGetting;
		public readonly Expression IndexGetting;

		public ArrayCellAccessExpression(Expression arrayGetting, Expression indexGetting, PositionInSources pos) : base(new TypeSpec(arrayGetting.ResultTypeSpec.Type), pos)
		{
			ArrayGetting = arrayGetting;
			IndexGetting = indexGetting;
		}

		private static TypeSpec RemoveArrayQualifier(TypeSpec typeSpec)
		{
			if (!typeSpec.QualifiersExists || typeSpec.Qualifiers[0].Kind != Qualifier.QKind.Array)
				throw new NotImplementedException();

			if (typeSpec.Qualifiers.Length == 1) return new TypeSpec(typeSpec.Type);

			return new TypeSpec(typeSpec.Type, typeSpec.Qualifiers[1..]);
		}
	}
}
