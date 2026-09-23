using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class DestructorStatement : Statement, IFunctionalBlockStatement
	{
		public readonly DestructorDesc DestructorSignature;
		private readonly TypeSpec _voidType;

		public DestructorStatement(DestructorDesc destructorSignature, IReadOnlyList<ILogicModel> childs, TypeSpec voidType, PositionInSources pos) 
			: base(childs, pos)
		{
			DestructorSignature = destructorSignature;
			_voidType = voidType;
		}

		public TypeSpec ReturnType => _voidType;

		public Argument[] Arguments => [];

		public IFuncRefData RefData => DestructorSignature.RefData;

		public string Name => "dtor";

		public override bool InevitableTerminating => LastStatementIsTerminating();
	}
}
