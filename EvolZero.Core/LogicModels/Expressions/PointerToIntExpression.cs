using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class PointerToIntExpression : Expression
	{
		public readonly Expression PointerGetting;

		public PointerToIntExpression(Expression pointerGetting, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			PointerGetting = pointerGetting;
		}
	}
}
