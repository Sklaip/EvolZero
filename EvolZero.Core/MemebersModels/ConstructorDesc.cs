using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class ConstructorDesc
	{
		public readonly Argument[] Arguments;
		public readonly IFuncRefData RefData;
		public readonly AccessModifier Access;

		public ConstructorDesc(Argument[] arguments, IFuncRefData refData, AccessModifier access)
		{
			Arguments = arguments;
			RefData = refData;
			Access = access;
		}

	}
}
