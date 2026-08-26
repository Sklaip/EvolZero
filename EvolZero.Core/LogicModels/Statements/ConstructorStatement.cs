using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.LogicModels.Statements
{
	public class ConstructorStatement : Statement, IFunctionalBlockStatement
	{
		public readonly ConstructorDesc ConstuctorSignature;
		private readonly TypeSpec _voidType;

		public ConstructorStatement(ConstructorDesc constructorSignature, IReadOnlyCollection<ILogicModel> childs, TypeSpec voidType, PositionInSources pos) : base(childs, pos)
		{
			ConstuctorSignature = constructorSignature;
			_voidType = voidType;
		}

		public TypeSpec ReturnType => _voidType;

		public Argument[] Arguments => ConstuctorSignature.Arguments;

		public IFuncRefData RefData => ConstuctorSignature.RefData;

		public string Name => "ctor";
	}
}
