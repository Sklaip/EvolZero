using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class DestructPointerExpression : Expression
	{
		public readonly Expression Expr;

		public DestructPointerExpression(Expression expression) : base(expression.ResultTypeSpec, expression.Pos)
		{
			Expr = expression;
		}
	}
}
