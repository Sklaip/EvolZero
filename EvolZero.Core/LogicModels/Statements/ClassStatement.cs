using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class ClassStatement : Statement
	{
		public readonly TypeDesc TypeDesc;
		public ClassStatement(TypeDesc typeDesc, IReadOnlyCollection<ILogicModel> childs, PositionInSources pos) : base(childs, pos)
		{
			TypeDesc = typeDesc;
		}
	}
}
