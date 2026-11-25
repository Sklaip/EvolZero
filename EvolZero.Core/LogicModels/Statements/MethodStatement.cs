using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class MethodStatement : Statement, IFunctionalBlockStatement
	{
		public readonly MethodDesc FunctionSignature;

		public MethodStatement(MethodDesc methodSignature, IReadOnlyList<ILogicModel> childs, PositionInSources pos) : base(childs, pos)
		{
			FunctionSignature = methodSignature;
		}

		public TypeSpec ReturnType => FunctionSignature.ReturnType;

		public Argument[] Arguments => FunctionSignature.Arguments;

		public string Name => FunctionSignature.Name;

		public override bool InevitableTerminating => LastStatementIsTerminating();

		public LifetimeDecl[] Lifetimes => FunctionSignature.Lifetimes;

		public IFunctionalBlockDesc BlockDesc => FunctionSignature;
	}
}
