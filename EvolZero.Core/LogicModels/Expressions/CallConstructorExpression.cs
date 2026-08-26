using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallConstructorExpression : Expression
	{
		public readonly Expression MemoryGetting;

		public readonly ConstructorDesc Constructor;
		public readonly Expression[] Arguments;


		public CallConstructorExpression(Expression memoryGetting, ConstructorDesc constructor, Expression[] arguments, PositionInSources pos) : base(memoryGetting.ResultTypeSpec, pos)
		{
			MemoryGetting = memoryGetting;
			Constructor = constructor;
			Arguments = arguments;
		}
	}
}
