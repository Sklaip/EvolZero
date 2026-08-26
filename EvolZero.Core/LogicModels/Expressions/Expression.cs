using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public abstract class Expression : ILogicModel
	{
		public readonly TypeSpec ResultTypeSpec;
		public readonly PositionInSources Pos;

		protected Expression(TypeSpec resultTypeSpec, PositionInSources pos)
		{
			ResultTypeSpec = resultTypeSpec;
			Pos = pos;
		}
	}
}
