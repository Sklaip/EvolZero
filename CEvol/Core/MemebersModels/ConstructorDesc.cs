using EvolZero.Generation;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class ConstructorDesc
	{
		public readonly Argument[] Arguments;
		public readonly FuncRefData RefData;
		public readonly AccessModifier Access;

		public ConstructorDesc(Argument[] arguments, FuncRefData refData, AccessModifier access)
		{
			Arguments = arguments;
			RefData = refData;
			Access = access;
		}

	}
}
