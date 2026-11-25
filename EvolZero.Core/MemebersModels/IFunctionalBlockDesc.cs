using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public interface IFunctionalBlockDesc
	{
		string Name { get; }
		Argument[] Arguments { get; }
		TypeSpec? ImplicitArgument { get; }
		bool IsInfArgs { get; }
		TypeSpec ReturnType { get; }
	}
}
