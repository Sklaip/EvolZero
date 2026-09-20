using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class GetPointerToVarExpression : Expression
	{
		public readonly Expression Variable;
		public readonly bool IsOwner;

		public GetPointerToVarExpression(Expression variable, bool isOwner, PositionInSources pos) 
			: base(PointerTypeSpec(variable.ResultTypeSpec, isOwner), pos)
		{
			Variable = variable;
			IsOwner = isOwner;
		}

		private static TypeSpec PointerTypeSpec(TypeSpec typeScec, bool isOwner)
		{
			return new TypeSpec(typeScec.Type, [isOwner ? Qualifier.Reference : Qualifier.BorrowReference, .. typeScec.Qualifiers]);
		}
	}
}
