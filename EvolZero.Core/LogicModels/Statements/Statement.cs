using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public abstract class Statement : ILogicModel
	{
		public readonly IReadOnlyCollection<ILogicModel> Childs;
		public readonly PositionInSources Pos;

		protected Statement(IReadOnlyCollection<ILogicModel> childs, PositionInSources pos)
		{
			Childs = childs;
			Pos = pos;
		}
	}
}
