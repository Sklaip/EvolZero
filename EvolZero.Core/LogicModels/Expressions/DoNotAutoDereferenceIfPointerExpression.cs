using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class DoNotAutoDereferenceIfPointerExpression : Expression
	{
		public Expression Expression { get; }
		public bool IsBorrow { get; }

		public DoNotAutoDereferenceIfPointerExpression(Expression expr, PositionInSources pos)
			: this(expr, false, pos)
		{
		}

		public DoNotAutoDereferenceIfPointerExpression(Expression expr, bool isBorrow, PositionInSources pos)
			: base(expr.ResultTypeSpec, pos)
		{
			Expression = expr;
			IsBorrow = isBorrow;
		}
	}
}
