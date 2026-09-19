using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;

namespace EvolZero.Core.Analysis
{
	public interface ILifitemesBypassConsumer
	{
		void EnterToIfStatement(IfStatement statement);
		void ExitFromIfStatement(IfStatement statement);
		void HandleConditionSubStatement(Statement statement);
		void GiveAwayOwnershipToRef(Expression expr);
		Expression? DestructPointer(Expression pointerExpr);
	}
}
