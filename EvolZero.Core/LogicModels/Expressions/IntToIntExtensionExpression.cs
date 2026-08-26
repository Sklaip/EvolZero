using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class IntToIntExtensionExpression : Expression
	{
		public readonly Expression NumGetting;
		public readonly bool IsSigned;

		public IntToIntExtensionExpression(Expression numGetting, bool isSigned, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			NumGetting = numGetting;
			IsSigned = isSigned;
		}
	}
}
