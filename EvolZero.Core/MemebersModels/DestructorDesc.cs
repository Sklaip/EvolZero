using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class DestructorDesc : IFunctionalBlockDesc
	{
		public readonly AccessModifier Access;

		public DestructorDesc(AccessModifier access)
		{
			Access = access;
		}

	}
}
