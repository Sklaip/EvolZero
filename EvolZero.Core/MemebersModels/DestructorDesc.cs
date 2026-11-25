namespace EvolZero.Core.MemebersModels
{
	public class DestructorDesc : IFunctionalBlockDesc
	{
		public Argument[] Arguments => [];

		public string Name => "dtor";

		public bool IsInfArgs => false;

		public TypeSpec ReturnType => _voidType;

		public TypeSpec? ImplicitArgument { get; private set; }

		public readonly AccessModifier Access;
		private readonly TypeSpec _voidType;

		public DestructorDesc(AccessModifier access, TypeDesc declaringType, TypeSpec voidType)
		{
			Access = access;
			_voidType = voidType;
			ImplicitArgument = new TypeSpec(declaringType, [Qualifier.BorrowReference]);
		}

	}
}
