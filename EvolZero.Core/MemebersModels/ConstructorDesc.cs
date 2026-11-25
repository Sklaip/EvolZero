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

		public TypeSpec? ImplicitArgument { get; private set; }

		public readonly AccessModifier Access;
		public readonly LifetimeDecl[] Lifetimes;
		private readonly TypeSpec _voidType;

		public ConstructorDesc(Argument[] arguments, AccessModifier access, LifetimeDecl[] lifetimes, TypeDesc declaringType, TypeSpec voidType)
		{
			Arguments = arguments;
			Access = access;
			Lifetimes = lifetimes;
			_voidType = voidType;
			ImplicitArgument = new TypeSpec(declaringType, [Qualifier.BorrowReference]);
		}

	}
}
