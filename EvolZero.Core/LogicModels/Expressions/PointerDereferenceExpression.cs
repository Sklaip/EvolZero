using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class PointerDereferenceExpression : Expression
	{
		public readonly Expression Target;

		public PointerDereferenceExpression(Expression target, PositionInSources pos) : base(RemovePointerQualifier(target.ResultTypeSpec), pos)
		{
			Target = target;
		}

		private static TypeSpec RemovePointerQualifier(TypeSpec typeSpec)
		{
			if (!typeSpec.QualifiersExists || typeSpec.Qualifiers[0].Kind != Qualifier.QKind.Reference)
				throw new NotImplementedException();

			if (typeSpec.Qualifiers.Length == 1) return new TypeSpec(typeSpec.Type);

			return new TypeSpec(typeSpec.Type, typeSpec.Qualifiers[1..]);
		}
	}
}
