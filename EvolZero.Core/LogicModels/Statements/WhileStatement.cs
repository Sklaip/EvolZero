using EvolZero.Core.LogicModels.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class WhileStatement : Statement
	{
		public readonly Expression Condition;
		public override bool InevitableTerminating { get; } = false; 

		public WhileStatement(IReadOnlyList<ILogicModel> childs, Expression condition, PositionInSources pos) : base(childs, pos)
		{
			Condition = condition;
		}
	}
}
