using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallDesructorExpression : Expression
	{
		public readonly Expression MemoryGetting;

		public readonly DestructorDesc Destructor;

		public CallDesructorExpression(Expression memoryGetting, DestructorDesc destructor, PositionInSources pos) 
			: base(memoryGetting.ResultTypeSpec, pos)
		{
			MemoryGetting = memoryGetting;
			Destructor = destructor;
		}
	}
}
