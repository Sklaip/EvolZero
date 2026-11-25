using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class TypeDesc
	{
		public readonly string Name;
		public readonly Dictionary<string, VariableDesc> Variables;
		public readonly Dictionary<string, MethodDesc[]> Functions;
		public readonly List<ConstructorDesc> Constructors;
		public readonly List<DestructorDesc> Destructors;
		public readonly List<TypeDesc> InheritedTypes = [];
		public readonly List<TypeDesc> CanExpandedTo = [];
		public readonly List<GenericParamDesc> GenericParams = [];

		public readonly bool IsBaseType = false;
		public bool IsGeneric { get => GenericParams.Count > 0; }

		public TypeDesc(string name, Dictionary<string, VariableDesc> variables,
			Dictionary<string, MethodDesc[]> functions, List<ConstructorDesc> constructors, List<DestructorDesc> destructors)
		{
			Name = name;
			Variables = variables;
			Functions = functions;
			Constructors = constructors;
			Destructors = destructors;
		}

		public TypeDesc(string name)
		{
			Name = name;
			Variables = [];
			Functions = [];
			Constructors = [];
			Destructors = [];
			IsBaseType = true;
		}
	}
}
