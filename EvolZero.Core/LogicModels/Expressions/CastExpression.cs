using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CastExpression : Expression
	{
		public readonly Expression Expression;

		public CastExpression(Expression expression, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Expression = expression;
		}
	}
}
