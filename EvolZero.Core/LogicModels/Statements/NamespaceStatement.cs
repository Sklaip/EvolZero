using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class NamespaceStatement : Statement
	{
		public readonly string NameSpace;

		public NamespaceStatement(string nameSpace, IReadOnlyCollection<ILogicModel> childs, PositionInSources pos) : base(childs, pos)
		{
			NameSpace = nameSpace;
		}
	}
}
