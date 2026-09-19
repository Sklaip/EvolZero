using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class VariableAccessExpression : Expression, IInitializableExpresion
	{
		public readonly string Name;
		public bool IsInitialized { get; set; } = false;
		public VariableAccessExpression(string name, TypeSpec resultTypeSpec, bool isInitialized, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Name = name;
			IsInitialized = isInitialized;
		}
	}
}
