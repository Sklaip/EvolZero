using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class BlockStatement : Statement
	{
		public BlockStatement(IReadOnlyList<ILogicModel> childs, PositionInSources pos) : base(childs, pos)
		{
		}
	}
}
