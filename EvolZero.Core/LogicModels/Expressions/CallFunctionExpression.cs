using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallFunctionExpression : Expression
	{
		public readonly FuncDesc Function;
		public readonly Expression[] Arguments;

		public CallFunctionExpression(Expression[] arguments, FuncDesc function, PositionInSources pos) : base(function.ReturnType, pos)
		{
			Arguments = arguments;
			Function = function;
		}
	}
}
