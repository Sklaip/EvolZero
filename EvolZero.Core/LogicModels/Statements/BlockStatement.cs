using System;
using System.Collections.Generic;
using System.Text;
namespace EvolZero.Core.LogicModels.Statements
{
	public class BlockStatement : Statement
	{
		public bool IsUnsafe { get; init; }

		public BlockStatement(IReadOnlyList<ILogicModel> childs, PositionInSources pos, bool isUnsafe = false) : base(childs, pos)
		{
			IsUnsafe = isUnsafe;
		}
	}
}
