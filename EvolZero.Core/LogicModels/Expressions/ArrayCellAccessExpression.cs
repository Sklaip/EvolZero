using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class ArrayCellAccessExpression : Expression
	{
		public readonly Expression ArrayGetting;
		public readonly Expression IndexGetting;

		public ArrayCellAccessExpression(Expression arrayGetting, Expression indexGetting, PositionInSources pos) 
			: base(arrayGetting.ResultTypeSpec.RemoveFirtsQualifier(), pos)
		{
			ArrayGetting = arrayGetting;
			IndexGetting = indexGetting;
		}
	}
}
