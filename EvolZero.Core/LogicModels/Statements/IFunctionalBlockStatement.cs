using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public interface IFunctionalBlockStatement
	{
		TypeSpec ReturnType { get; }
		Argument[] Arguments { get; }
		IFuncRefData RefData { get; }
		string Name { get; }
	}
}
