using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class VariableCreatingExpression : Expression, IInitializableExpresion
	{
		public readonly string Name;
		public bool IsInitialized { get; set; } = false;

		public VariableCreatingExpression(string name, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Name = name;
		}
	}
}

