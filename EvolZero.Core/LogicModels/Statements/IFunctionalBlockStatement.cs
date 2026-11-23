using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Statements
{
	public interface IFunctionalBlockStatement
	{
		TypeSpec ReturnType { get; }
		Argument[] Arguments { get; }
		IFunctionalBlockDesc BlockDesc { get; }
		LifetimeDecl[] Lifetimes { get; }
		string Name { get; }
	}
}
