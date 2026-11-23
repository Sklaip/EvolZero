using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Statements
{
	public class ConstructorStatement : Statement, IFunctionalBlockStatement
	{
		public readonly ConstructorDesc ConstuctorSignature;
		private readonly TypeSpec _voidType;

		public ConstructorStatement(ConstructorDesc constructorSignature, IReadOnlyList<ILogicModel> childs, TypeSpec voidType, PositionInSources pos) : base(childs, pos)
		{
			ConstuctorSignature = constructorSignature;
			_voidType = voidType;
		}

		public TypeSpec ReturnType => _voidType;

		public Argument[] Arguments => ConstuctorSignature.Arguments;

		public string Name => "ctor";

		public override bool InevitableTerminating => LastStatementIsTerminating();

		public LifetimeDecl[] Lifetimes => ConstuctorSignature.Lifetimes;

		public IFunctionalBlockDesc BlockDesc => ConstuctorSignature;
	}
}
