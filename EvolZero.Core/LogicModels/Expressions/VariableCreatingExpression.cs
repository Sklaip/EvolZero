using EvolZero.Core.MemebersModels;

namespace EvolZero.Core.LogicModels.Expressions
{
	public class VariableCreatingExpression : Expression, IInitializableExpresion
	{
		public readonly string Name;
		public bool IsInitialized { get; set; } = false;
		public readonly bool IsTemp;


        public VariableCreatingExpression(string name, TypeSpec resultTypeSpec, bool isTemp, PositionInSources pos) : base(resultTypeSpec, pos)
		{
			Name = name;
            IsTemp = isTemp;
        }
	}
}

