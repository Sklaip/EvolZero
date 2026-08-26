using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class FloatToIntExpression : Expression
	{
		public readonly Expression NumGetting;
		public readonly bool IsSigned;

		public FloatToIntExpression(Expression numGetting, bool isSigned, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			NumGetting = numGetting;
			IsSigned = isSigned;
		}
	}
}
