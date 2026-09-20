using EvolZero.Core.LogicModels;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using System.Reflection;

namespace EvolZero.Core.Analysis
{
	internal class LiftimesConsumer : ILifitemesBypassConsumer
	{
		class ConditionBlock(IfStatement statement)
		{
			public IfStatement Statement = statement;
			public List<(Expression refExpr, Statement SubStatement)> GivenRefs = new();
			public Statement CurrentSubStatement;
		}

		private readonly Stack<ConditionBlock> _blocks = new();

		public void EnterToIfStatement(IfStatement statement)
		{
			_blocks.Push(new ConditionBlock(statement));
		}

		public void ExitFromIfStatement(IfStatement statement)
		{
			var condBlock = _blocks.Pop();

			foreach (var givenRef in condBlock.GivenRefs)
			{
				if (givenRef.SubStatement == statement)
				{
					if (statement.ElseStatement == null)
						statement.ElseStatement = new BlockStatement(new List<ILogicModel>(), statement.Pos);

					AddPointerDestruct(givenRef.refExpr, statement.ElseStatement, statement.ElseIfStatements);
				}
				else if (givenRef.SubStatement == statement.ElseStatement && statement.ElseStatement != null)
				{
					AddPointerDestruct(givenRef.refExpr, statement, statement.ElseIfStatements);
				}
				else if (statement.ElseIfStatements?.Any(x => x == givenRef.SubStatement) != null)
				{
					var elseIfs = statement.ElseIfStatements?.Where(x => x != givenRef.SubStatement);
					AddPointerDestruct(givenRef.refExpr, statement, statement.ElseIfStatements);

					if (statement.ElseStatement == null)
					{
						statement.ElseStatement = new BlockStatement(new List<ILogicModel>(), statement.Pos);
						statement.ElseStatement.AddLogicModel(new DestructPointerExpression(givenRef.refExpr));
					}
				}
				else
				{
					throw new NotImplementedException();
				}
			}
		}

		private void AddPointerDestruct(Expression pointer, Statement statement, IEnumerable<Statement>? otherStatements)
		{
			if (otherStatements != null)
			{
				foreach (var st in otherStatements)
				{
					st.AddLogicModel(new DestructPointerExpression(pointer));
				}
			}

			statement.AddLogicModel(new DestructPointerExpression(pointer));
		}

		public void HandleConditionSubStatement(Statement statement)
		{
			var currentIf = _blocks.Peek();
			currentIf.CurrentSubStatement = statement;
		}

		public void GiveAwayOwnershipToRef(Expression expr)
		{
			if (!_blocks.TryPeek(out var currentIf)) return;
			currentIf.GivenRefs.Add((expr, currentIf.CurrentSubStatement));
		}

		public Expression? DestructPointer(Expression pointerExpr)
		{
			return new DestructPointerExpression(pointerExpr); // TODO: emitter потом просто повторно въебет этот Statement (если например это был new), поэтому нужно сохранять в какую-нибудь анонимную ссылку
		}

	}
}
