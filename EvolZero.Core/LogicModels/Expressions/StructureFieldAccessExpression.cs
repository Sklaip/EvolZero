using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class StructureFieldAccessExpression : Expression
	{
		public readonly uint FiledNum;
		public readonly VariableDesc Field;
		public readonly Expression StructureGetting;

		public StructureFieldAccessExpression(VariableDesc field, Expression structureGetting, TypeSpec resultTypeSpec, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			FiledNum = field.Order;
			Field = field;
			StructureGetting = structureGetting;
		}
	}
}
