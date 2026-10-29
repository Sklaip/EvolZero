using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class ObjectInitializerExpression : Expression
	{
		public Expression ObjectToInit { get;set;  }
		public List<Expression> Expressions { get; set; }

		public ObjectInitializerExpression(List<Expression> expressions, AnonymousObjectTypeDesc resultType, PositionInSources pos) 
			: base(new TypeSpec(resultType), pos)
		{
			Expressions = expressions;
			ObjectToInit = null!;
		}

		public ObjectInitializerExpression(Expression objectToInit, List<Expression> expressions, PositionInSources pos) : base(objectToInit.ResultTypeSpec, pos)
		{
			Expressions = expressions;
			ObjectToInit = objectToInit;
		}

		public ObjectInitializerExpression RecreateToObject(Expression objectToInit, Func<Expression, int, Expression> getExpressionWrapper)
		{
			var newList = Expressions.ToList();

			for (int i = 0; i < newList.Count; i++)
			{
				Expression? item = newList[i];
				newList[i] = getExpressionWrapper(item, i);
			}

			return new ObjectInitializerExpression(objectToInit, newList, Pos);
		}
	}
}
