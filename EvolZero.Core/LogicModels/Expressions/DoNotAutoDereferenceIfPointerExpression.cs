using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class DoNotAutoDereferenceIfPointerExpression : Expression
	{
		public Expression Expression { get; }

		public DoNotAutoDereferenceIfPointerExpression(Expression expr, PositionInSources pos) : base(expr.ResultTypeSpec, pos)
		{
			Expression = expr;
		}
	}
}
