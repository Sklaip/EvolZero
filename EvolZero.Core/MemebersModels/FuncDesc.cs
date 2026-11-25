using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class FuncDesc : IFunctionalBlockDesc
	{
		public TypeSpec ReturnType { get; private set; }
		public string Name { get; private set; }
		public Argument[] Arguments { get; private set; }
		public bool IsInfArgs { get; private set; }

		public TypeSpec? ImplicitArgument => null;

		public readonly AccessModifier Access;
		public readonly LifetimeDecl[] Lifetimes;


		public FuncDesc(TypeSpec returnType, string name, Argument[] arguments, bool isInfArgs, 
			AccessModifier access, LifetimeDecl[] lifetimes)
		{
			ReturnType = returnType;
			Name = name;
			Arguments = arguments;
			IsInfArgs = isInfArgs;
			Access = access;
			Lifetimes = lifetimes;
		}

	}
}
