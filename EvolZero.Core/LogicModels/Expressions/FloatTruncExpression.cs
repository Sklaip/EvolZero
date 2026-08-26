using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class FloatTruncExpression : Expression
	{
		public readonly Expression NumGetting;

		public FloatTruncExpression(Expression numGetting, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			NumGetting = numGetting;
		}
	}
}
