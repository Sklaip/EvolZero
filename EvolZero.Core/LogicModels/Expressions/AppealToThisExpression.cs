using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class AppealToThisExpression : Expression
	{
		public AppealToThisExpression(TypeDesc cls, PositionInSources pos) : base(new TypeSpec(cls, [new Qualifier(Qualifier.QKind.Reference)]), pos)
		{
		}
	}
}
