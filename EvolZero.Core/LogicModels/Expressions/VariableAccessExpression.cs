using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class VariableAccessExpression : Expression
	{
		public readonly string Name;
		public VariableAccessExpression(string name, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Name = name;
		}
	}
}
