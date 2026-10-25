using EvolZero.Core.Analysis.Semantic;
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
			public List<(Expression refExpr, VarMeta? Var, Statement SubStatement)> GivenRefs = new();
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

			if (condBlock.GivenRefs.Count == 0) return;

			// Ветки конструкции if/else-if/else: тело if лежит в самом statement,
			// у else-if — отдельные IfStatement из ElseIfStatements, у else — свой блок.
			var branches = new List<Statement> { statement };
			branches.AddRange(statement.ElseIfStatements);
			if (statement.ElseStatement == null)
				statement.ElseStatement = new BlockStatement(new List<ILogicModel>(), statement.Pos);
			branches.Add(statement.ElseStatement);

			// Группируем отдачи владения по переменной: выражения из разных веток —
			// разные узлы AST, а VarMeta для одной переменной всегда один и тот же инстанс.
			// Для записей без VarMeta идентичностью служит само выражение.
			var groups = condBlock.GivenRefs.GroupBy(g => (object?)g.Var ?? g.refExpr);

			foreach (var group in groups)
			{
				// Ветки, которые сами отдали владение этой переменной, очистку не получают:
				// объект уже переехал в другое место (или его удалит получатель владения).
				var gaveAwayIn = group.Select(g => g.SubStatement).ToHashSet();
				var destructExpr = group.First().refExpr;

				foreach (var branch in branches)
				{
					if (gaveAwayIn.Contains(branch)) continue;
					branch.AddLogicModel(GetDestructor(destructExpr));
				}
			}
		}

		public void HandleConditionSubStatement(Statement statement)
		{
			var currentIf = _blocks.Peek();
			currentIf.CurrentSubStatement = statement;
		}

		public void GiveAwayOwnership(Expression expr, VarMeta? var, bool refersToCurrentBlock)
		{
			if (!_blocks.TryPeek(out var currentIf) || refersToCurrentBlock) return;
			currentIf.GivenRefs.Add((expr, var, currentIf.CurrentSubStatement));
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
