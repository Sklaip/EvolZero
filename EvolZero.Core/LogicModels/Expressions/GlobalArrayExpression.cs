using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class GlobalArrayExpression : Expression
	{
		public readonly byte[] Array;

		public GlobalArrayExpression(byte[] array, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Array = array;
		}
	}
}
