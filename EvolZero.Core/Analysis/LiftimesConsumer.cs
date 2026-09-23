using EvolZero.Core.LogicModels;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
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
					AddPointerDestruct(givenRef.refExpr, statement, elseIfs);

					if (statement.ElseStatement == null)
					{
						statement.ElseStatement = new BlockStatement(new List<ILogicModel>(), statement.Pos);
					}

					statement.ElseStatement.AddLogicModel(GetDestructor(givenRef.refExpr));
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
					st.AddLogicModel(GetDestructor(pointer));
				}
			}

			statement.AddLogicModel(GetDestructor(pointer));
		}

		public void HandleConditionSubStatement(Statement statement)
		{
			var currentIf = _blocks.Peek();
			currentIf.CurrentSubStatement = statement;
		}

		public void GiveAwayOwnership(Expression expr)
		{
			if (!_blocks.TryPeek(out var currentIf)) return;
			currentIf.GivenRefs.Add((expr, currentIf.CurrentSubStatement));
		}

		public Expression? PointerLifetimeEnd(Expression pointerExpr)
		{
			if (!pointerExpr.ResultTypeSpec.Type.IsBaseType)
			{
				return GetDestructor(pointerExpr);
			}

			return new DestructPointerExpression(pointerExpr); // TODO: emitter потом просто повторно въебет этот Statement (если например это был new), поэтому нужно сохранять в какую-нибудь анонимную ссылку
		}

		public List<Expression> HandleDestructor(TypeDesc typeDesc, DestructorStatement destructorStatement)
		{
			var pos = destructorStatement.Pos;
			var result = new List<Expression>();

			foreach (var field in typeDesc.Variables.Values)
			{
				if (field.Declaring.IsOwnerRef && !field.Declaring.Type.IsBaseType)
				{
					var thisGetting = new PointerDereferenceExpression(new AppealToThisExpression(typeDesc, pos), pos);
					Expression fieldAccess = new StructureFieldAccessExpression(field, thisGetting, field.Declaring, true, pos);

					result.Add(GetDestructor(fieldAccess));
				}
			}

			result.Add(new DestructPointerExpression(new AppealToThisExpression(typeDesc, pos)));

			return result;
		}

		private CallDesructorExpression GetDestructor(Expression expr)
		{
			var typeDestructor = expr.ResultTypeSpec.Type.Destructors.First();
			return new CallDesructorExpression(expr, typeDestructor, expr.Pos);
		}

	}
}
