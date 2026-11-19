using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class GenericParamDesc : TypeDesc
	{
		public readonly string ParamName;

		public GenericParamDesc(string name, ITypeRef typeRef, Dictionary<string, VariableDesc> variables, Dictionary<string, FuncDesc[]> functions, List<ConstructorDesc> constructors, List<DestructorDesc> destructors) : base(name, typeRef, variables, functions, constructors, destructors)
		{
		}
	}
}
