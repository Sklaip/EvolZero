using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class AllocateHeapMemoryToType : Expression
	{
		public AllocateHeapMemoryToType(TypeSpec resultTypeSpec, PositionInSources pos = default) : base(resultTypeSpec, pos)
		{

		}
	}
}
