using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class CallMethodExpression : Expression
	{
		public readonly MethodDesc Method;
		public readonly Expression[] Arguments;
		public readonly Expression InstanceGetting;

		public CallMethodExpression(Expression InstanceGetting, Expression[] arguments, MethodDesc method, PositionInSources pos) 
			: base(method.ReturnType, pos)
		{
			this.InstanceGetting = InstanceGetting;
			Arguments = arguments;
			Method = method;
		}
	}

}
