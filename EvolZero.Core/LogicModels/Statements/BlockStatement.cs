using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class BlockStatement : Statement
	{
		public BlockStatement(IReadOnlyCollection<ILogicModel> childs, PositionInSources pos) : base(childs, pos)
		{
		}
	}
}
