using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class ObjectInitializerExpression : Expression
	{
		public bool ByRef { get; set; }
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

		public ObjectInitializerExpression RecreateToObject(Expression objectToInit, bool byRef = false)
		{
			return new ObjectInitializerExpression(objectToInit, Expressions.ToList(), Pos)
			{
				ByRef = byRef
            };
		}
	}
}
