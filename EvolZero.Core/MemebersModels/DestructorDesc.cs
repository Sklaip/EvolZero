using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class DestructorDesc : IFunctionalBlockDesc
	{
		public Argument[] Arguments => [];

		public string Name => "dtor";

		public bool IsInfArgs => false;

		public TypeSpec ReturnType => _voidType;

		public readonly AccessModifier Access;
		private readonly TypeSpec _voidType;

		public DestructorDesc(AccessModifier access, TypeSpec voidType)
		{
			Access = access;
			_voidType = voidType;
		}

	}
}
