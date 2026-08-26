using EvolZero.Core.LogicModels.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class IfStatement : Statement
	{
		public readonly Expression Condition;
		public readonly IfStatement? IfElseStatement;
		public readonly Statement? ElseStatement;

		public IfStatement(IReadOnlyCollection<ILogicModel> childs, Expression condition, IfStatement? ifElseStatement, Statement? elseStatement, PositionInSources pos) : base(childs, pos)
		{
			Condition = condition;
			IfElseStatement = ifElseStatement;
			ElseStatement = elseStatement;
		}
	}
}
