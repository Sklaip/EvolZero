using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class SimpleBinaryOperationExpression : Expression
	{
		public readonly Expression LeftExpression;
		public readonly Expression RightExpression;
		public readonly BinaryOperation OperationType;

		public SimpleBinaryOperationExpression(BinaryOperation operationType, Expression leftExpression, Expression rightExpression, TypeSpec resultTypeSpec, PositionInSources pos)
			: base(new TypeSpec(resultTypeSpec.Type), pos)
		{
			LeftExpression = leftExpression;
			RightExpression = rightExpression;
			OperationType = operationType;
		}
	}
}
