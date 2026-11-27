using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallDestructorExpression : Expression
	{
		public readonly Expression MemoryGetting;

		public readonly DestructorDesc Destructor;

		public CallDestructorExpression(Expression memoryGetting, DestructorDesc destructor, PositionInSources pos) 
			: base(memoryGetting.ResultTypeSpec, pos)
		{
			MemoryGetting = memoryGetting;
			Destructor = destructor;
		}
	}
}
