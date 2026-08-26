using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class AllocateHeapMemoryToType : Expression
	{
		public readonly Expression? Multiper;

		public AllocateHeapMemoryToType(TypeSpec resultTypeSpec, Expression? multiper = null, PositionInSources pos = default) : base(resultTypeSpec, pos)
		{
			Multiper = multiper;
		}
	}
}
