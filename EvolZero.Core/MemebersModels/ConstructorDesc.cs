using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class ConstructorDesc : IFunctionalBlockDesc
	{
		public readonly Argument[] Arguments;
		public readonly AccessModifier Access;
		public readonly LifetimeDecl[] Lifetimes;

		public ConstructorDesc(Argument[] arguments, AccessModifier access, LifetimeDecl[] lifetimes)
		{
			Arguments = arguments;
			Access = access;
			Lifetimes = lifetimes;
		}

	}
}
