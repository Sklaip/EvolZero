using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class DestructorDesc
	{
		public readonly IFuncRefData RefData;
		public readonly AccessModifier Access;

		public DestructorDesc(IFuncRefData refData, AccessModifier access)
		{
			RefData = refData;
			Access = access;
		}

	}
}
