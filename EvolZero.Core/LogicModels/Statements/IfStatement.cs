using EvolZero.Core.LogicModels.Expressions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class IfStatement : Statement
	{
		public readonly Expression Condition;
		public Statement? ElseStatement { get; set; }

		private List<IfStatement> _elseIfStatements = new List<IfStatement>();

		public IReadOnlyCollection<IfStatement> ElseIfStatements { get => _elseIfStatements; }

		public IfStatement(IReadOnlyCollection<ILogicModel> childs, Expression condition, PositionInSources pos) : base(childs, pos)
		{
			Condition = condition;
		}

		public void AddElseIf(IfStatement statement)
		{
			_elseIfStatements.Add(statement);
		}

		public IfStatement Decompose()
		{
			if (_elseIfStatements.Count == 0) return this;

			var fisrtsElseIf = _elseIfStatements[0];
			var decreasedStatement = new IfStatement(fisrtsElseIf.Childs, fisrtsElseIf.Condition, fisrtsElseIf.Pos)
			{
				ElseStatement = ElseStatement,
				_elseIfStatements = _elseIfStatements.Skip(1).ToList()
			};

			return decreasedStatement;
		}
	}
}
