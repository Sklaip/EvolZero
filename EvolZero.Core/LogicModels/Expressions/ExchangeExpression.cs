using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class ExchangeExpression : Expression
	{
		public readonly Expression Target;
		public readonly Expression Value;

		public ExchangeExpression(Expression target, Expression value, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Target = target;
			Value = value;
		}
	}
}