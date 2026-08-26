using EvolZero.Core.LogicModels.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class WhileStatement : Statement
	{
		public readonly Expression Condition;

		public WhileStatement(IReadOnlyCollection<ILogicModel> childs, Expression condition, PositionInSources pos) : base(childs, pos)
		{
			Condition = condition;
		}
	}
}
