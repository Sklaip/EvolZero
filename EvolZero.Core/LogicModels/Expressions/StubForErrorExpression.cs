using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class StubForErrorExpression : Expression
	{
		public StubForErrorExpression(PositionInSources pos) : base(new TypeSpec(new TypeDesc("ERROR", null!)), pos)
		{
		}
	}
}
