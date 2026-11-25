namespace EvolZero.Core.MemebersModels
{
	public class GenericParamDesc : TypeDesc
	{
		public readonly string ParamName;
		public readonly int ParamIndex;

		public GenericParamDesc(string name, ITypeRef typeRef, Dictionary<string, VariableDesc> variables, Dictionary<string, MethodDesc[]> functions, List<ConstructorDesc> constructors, List<DestructorDesc> destructors, string paramName, int paramIndex) 
			: base(name, variables, functions, constructors, destructors)
		{
			ParamName = paramName;
			ParamIndex = paramIndex;
		}
	}
}
