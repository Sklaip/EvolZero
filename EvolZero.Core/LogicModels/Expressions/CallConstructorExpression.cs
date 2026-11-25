using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallConstructorExpression : Expression
	{
		public readonly Expression MemoryGetting;

		public readonly ConstructorDesc Constructor;
		public readonly Expression[] Arguments;
		public readonly bool ToHeap;

		public CallConstructorExpression(Expression memoryGetting, ConstructorDesc constructor, 
			Expression[] arguments, bool toHeap, PositionInSources pos) : base(memoryGetting.ResultTypeSpec, pos)
		{
			MemoryGetting = memoryGetting;
			Constructor = constructor;
			Arguments = arguments;
            ToHeap = toHeap;
        }
	}
}
