using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.Analysis
{
	public interface ILifitemesBypassConsumer
	{
		void EnterToIfStatement(IfStatement statement);
		void ExitFromIfStatement(IfStatement statement);
		void HandleConditionSubStatement(Statement statement);
		void GiveAwayOwnership(Expression expr);
		Expression? PointerLifetimeEnd(Expression pointerExpr);
		List<Expression> HandleDestructor(TypeDesc typeDesc, DestructorStatement destructorStatement);
	}
}
