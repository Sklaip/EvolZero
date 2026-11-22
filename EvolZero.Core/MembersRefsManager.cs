using EvolZero.Core.MemebersModels;

namespace EvolZero.Core
{
	public class MembersRefsManager
	{
		private readonly IPlatformTypesProvider _typesProvider;
		private static readonly TypeRefStub _stub = new();

		/// <summary>
		/// <see cref="ITypeRef"/> мы храним не на каждый TypeSpec, 
		/// а только на уникальную связку Type и списка generic параметров
		/// </summary>
		private Dictionary<TypeSpec, ITypeRef> _typesList;

		public MembersRefsManager(IPlatformTypesProvider typesProvider, Dictionary<TypeSpec, ITypeRef> baseTypes)
		{
			_typesProvider = typesProvider;
			_typesList = baseTypes;
		}

		public void AddTypeSpec(TypeSpec typeSpec)
		{
			_typesList.TryAdd(GetKey(typeSpec), _stub);
		}

		public ITypeRef GetTypeRef(TypeSpec typeSpec)
		{
			return _typesList[GetKey(typeSpec)];
		}

		public ITypeRef GetRealTypeRef(TypeSpec typeSpec)
		{
			var baseType = GetTypeRef(typeSpec);
			if (!typeSpec.QualifiersExists) return baseType;

			if (typeSpec.Qualifiers.Length > 1)
				return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, typeSpec.Qualifiers.Skip(1), baseType);

			return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, [], baseType);
		}

		public void BuildSigantures(ICodeGenerator codeGenerator)
		{
			foreach (var (type, typeRef) in _typesList.ToArray())
			{
				CreateTypeBody(codeGenerator, type);
			}
		}

		private ITypeRef CreateTypeBody(ICodeGenerator codeGenerator, TypeSpec typeSpec)
		{
			ITypeRef typeRef;
			if (!typeSpec.Type.IsGeneric)
			{
				typeRef = codeGenerator.CreateStructure(typeSpec.Type.Name);
			}
			else
			{
				var genericParams = typeSpec.ConfiguredGenericParams;
				string genericNameEnding = string.Join("__", genericParams.Select((x, i) => $"gp{i}{x.ToString("_")}"));
				typeRef = codeGenerator.CreateStructure($"{typeSpec.Type.Name}__{genericNameEnding}");
			}

			_typesList[typeSpec] = typeRef;

			var fieldsTypesRefs = new List<ITypeRef>();

			var fields = typeSpec.Type.Variables.Values.OrderBy(x => x.Order);
			foreach (var field in fields)
			{
				var fieldDeclaring = field.Declaring;
				if (fieldDeclaring.Type is GenericParamDesc gpd)
				{
					fieldDeclaring = typeSpec.ConfiguredGenericParams[gpd.ParamIndex];
				}

				var simpleTypeSpec = GetKey(fieldDeclaring);
				var fieldTypeRef = _typesList[simpleTypeSpec];

				if (fieldTypeRef is TypeRefStub)
				{
					// Тело этого типа еще не создано, вызываем создание
					fieldTypeRef = CreateTypeBody(codeGenerator, simpleTypeSpec);
				}

				fieldsTypesRefs.Add(GetRealTypeRef(field.Declaring));
			}

			codeGenerator.FillStructureBody(typeRef, fieldsTypesRefs);

			return typeRef;
		}

		private TypeSpec GetKey(TypeSpec source)
		{
			return new TypeSpec(source.Type, [], source.ConfiguredGenericParams);
		}
	}
}
