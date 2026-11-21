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
		public readonly List<DestructorDesc> Destructors;
		public readonly List<TypeDesc> InheritedTypes = [];
		public readonly List<TypeDesc> CanExpandedTo = [];
		public readonly List<GenericParamDesc> GenericParams = [];

		private ITypeRef? _typeRef;
		public ITypeRef TypeRef
		{
			get => _typeRef ?? throw new InvalidOperationException("TypeRef has not been initialized.");
			set
			{
				if (_typeRef != null) throw new InvalidOperationException("Value elready exists.");
			}
		}

		public readonly bool IsBaseType = false;
		public bool IsGeneric { get => GenericParams.Count > 0; }

		public TypeDesc(string name, Dictionary<string, VariableDesc> variables,
			Dictionary<string, FuncDesc[]> functions, List<ConstructorDesc> constructors, List<DestructorDesc> destructors)
		{
			Name = name;
			Variables = variables;
			Functions = functions;
			Constructors = constructors;
			Destructors = destructors;
		}

		public TypeDesc(string name, ITypeRef typeRef)
		{
			Name = name;
			Variables = [];
			Functions = [];
			Constructors = [];
			Destructors = [];
			IsBaseType = true;
			TypeRef = typeRef;
		}
	}
}
