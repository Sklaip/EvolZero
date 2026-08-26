using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.MemebersModels
{
	public class TypeDesc
	{
		public readonly string Name;
		public readonly Dictionary<string, VariableDesc> Variables;
		public readonly Dictionary<string, FuncDesc[]> Functions;
		public readonly List<ConstructorDesc> Constructors;
		public readonly List<TypeDesc> InheritedTypes = [];
		public readonly List<TypeDesc> CanExpandedTo = [];

		public readonly ITypeRef TypeRef;

		public readonly bool IsBaseType = false;

		public TypeDesc(string name, ITypeRef typeRef, Dictionary<string, VariableDesc> variables, 
			Dictionary<string, FuncDesc[]> functions, List<ConstructorDesc> constructors)
		{
			Name = name;
			Variables = variables;
			Functions = functions;
			TypeRef = typeRef;
			Constructors = constructors;
		}

		public TypeDesc(string name, ITypeRef typeRef)
		{
			Name = name;
			Variables = [];
			Functions = [];
			Constructors = [];
			IsBaseType = true;
			TypeRef = typeRef;
		}
	}
}
