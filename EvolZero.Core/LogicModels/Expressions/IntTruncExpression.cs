using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class IntTruncExpression : Expression
	{
		public readonly Expression NumGetting;

		public IntTruncExpression(Expression numGetting, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			NumGetting = numGetting;
		}
	}
}
