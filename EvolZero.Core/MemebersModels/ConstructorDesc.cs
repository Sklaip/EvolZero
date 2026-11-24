using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class ConstructorDesc : IFunctionalBlockDesc
	{
		public Argument[] Arguments { get; private set; }

		public string Name => "ctor";

		public bool IsInfArgs => false;

		public TypeSpec ReturnType => _voidType;

		public readonly AccessModifier Access;
		public readonly LifetimeDecl[] Lifetimes;
		private readonly TypeSpec _voidType;

		public ConstructorDesc(Argument[] arguments, AccessModifier access, LifetimeDecl[] lifetimes, TypeSpec voidType)
		{
			Arguments = arguments;
			Access = access;
			Lifetimes = lifetimes;
			_voidType = voidType;
		}

	}
}
