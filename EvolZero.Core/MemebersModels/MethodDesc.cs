namespace EvolZero.Core.MemebersModels
{
	public class MethodDesc : IFunctionalBlockDesc
	{
		public string Name {get; private set;}

		public Argument[] Arguments { get; private set; }

		public bool IsInfArgs { get; private set; }

		public TypeSpec ReturnType { get; private set; }

		public TypeSpec? ImplicitArgument { get; private set; }

		public readonly AccessModifier Access;
		public readonly LifetimeDecl[] Lifetimes;

		public MethodDesc(TypeSpec returnType, string name, Argument[] arguments, bool isInfArgs,
			AccessModifier access, LifetimeDecl[] lifetimes, TypeDesc declaringType)
		{
			ReturnType = returnType;
			Name = name;
			Arguments = arguments;
			IsInfArgs = isInfArgs;
			Access = access;
			Lifetimes = lifetimes;
			ImplicitArgument = new TypeSpec(declaringType, [Qualifier.BorrowReference]);
		}
	}
}
