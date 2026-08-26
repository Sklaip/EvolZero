using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class AllocateStackMemoryToType : Expression
	{
		public readonly Expression? Multiper;

		public AllocateStackMemoryToType(TypeSpec resultTypeSpec, Expression? multiper = null, PositionInSources pos = default) : base(resultTypeSpec, pos)
		{
			Multiper = multiper;
		}
	}
}
