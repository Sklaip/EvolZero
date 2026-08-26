using EvolZero.Core.LogicModels.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class ReturnStatement : Statement
	{
		public readonly Expression Value;

		public ReturnStatement(Expression expr, PositionInSources pos) : base([], pos)
		{
			Value = expr;
		}
	}
}
