using EvolZero.Core.MemebersModels;

namespace EvolZero.Core
{
	public class MembersRefsManager
	{
		private readonly IPlatformTypesProvider _typesProvider;
		private static readonly TypeRefStub _typeStub = new();
		private static readonly FuncRefDataStub _funcStub = new();

		/// <summary>
		/// <see cref="ITypeRef"/> мы храним не на каждый TypeSpec, 
		/// а только на уникальную связку Type и списка generic параметров
		/// </summary>
		private Dictionary<TypeSpec, ITypeRef> _typesList;

		private Dictionary<IFunctionalBlockDesc, Dictionary<TypeSpec, (IFuncRefData refData, TypeSpec[] configuredArgs)>> _classMembersList = new();
		private Dictionary<IFunctionalBlockDesc, IFuncRefData> _singleFunctions = new();

		private readonly struct FuncArgs : IEquatable<FuncArgs>
		{
			public readonly TypeSpec[] Args;

			public FuncArgs(TypeSpec[] args)
			{
				Args = args ?? Array.Empty<TypeSpec>();
			}

			public bool Equals(FuncArgs other)
			{
				var a = Args ?? Array.Empty<TypeSpec>();
				var b = other.Args ?? Array.Empty<TypeSpec>();

				if (ReferenceEquals(a, b))
					return true;

				if (a.Length != b.Length)
					return false;

				for (int i = 0; i < a.Length; i++)
				{
					if (!Equals(a[i], b[i]))
						return false;
				}

				return true;
			}

			public override bool Equals(object? obj) => obj is FuncArgs other && Equals(other);

			public override int GetHashCode()
			{
				var a = Args ?? Array.Empty<TypeSpec>();
				var hash = new HashCode();

				for (int i = 0; i < a.Length; i++)
				{
					hash.Add(a[i]);
				}

				return hash.ToHashCode();
			}

			public static bool operator ==(FuncArgs left, FuncArgs right) => left.Equals(right);

			public static bool operator !=(FuncArgs left, FuncArgs right) => !left.Equals(right);
		}

		public MembersRefsManager(IPlatformTypesProvider typesProvider, MembersTable table, Dictionary<TypeDesc, ITypeRef> baseTypes)
		{
			_typesProvider = typesProvider;
			_typesList = baseTypes.Select(x => (new TypeSpec(x.Key), x.Value)).ToDictionary();

			foreach (var type in table.Types.Values)
			{
				if (type.HasGenericParams) continue;
				_typesList.TryAdd(new TypeSpec(type), _typeStub);

				AddClassMembers(type.Functions.Values.SelectMany(x => x), type);
				AddClassMembers(type.Constructors, type);
				AddClassMembers(type.Destructors, type);
			}

			AddFunctions(table.Functions.Values.SelectMany(x => x));
		}

		private void AddClassMembers(IEnumerable<IFunctionalBlockDesc> functions, TypeDesc parentType)
		{
			foreach (var func in functions)
			{
				if (!_classMembersList.TryGetValue(func, out var memberVariations))
				{
					memberVariations = new();
					_classMembersList[func] = memberVariations;
				}

				memberVariations[new TypeSpec(parentType)] = (_funcStub, []);
			}
		}

		private void AddFunctions(IEnumerable<IFunctionalBlockDesc> functions)
		{
			foreach (var func in functions)
			{
				_singleFunctions[func] = _funcStub;
			}
		}

		public void AddTypeIfGeneric(TypeSpec typeSpec)
		{
			if (typeSpec.ConfiguredGenericParams.Length > 0)
				_typesList.TryAdd(GetKey(typeSpec), _typeStub);
		}

		public void AddFuncIfGeneric(IFunctionalBlockDesc func, TypeSpec parentType)
		{
			parentType = GetKey(parentType);
			if (!parentType.Type.HasGenericParams) return;

			if (!_classMembersList.TryGetValue(func, out var memberVariations))
			{
				memberVariations = new();
				_classMembersList[func] = memberVariations;
			}

			var configuredArgs = new List<TypeSpec>();

			var sourceArgs = func.Arguments;
			for (int i = 0; i < sourceArgs.Length; i++)
			{
				var typeSpec = sourceArgs[i].Declaring;
				if (typeSpec.Type is not GenericParamDesc generic)
				{
					configuredArgs.Add(typeSpec);
				}
				else
				{
					TypeSpec currentConfiguedParameter = parentType.ConfiguredGenericParams[generic.ParamIndex];
					configuredArgs.Add(currentConfiguedParameter);
				}
			}

			memberVariations[parentType] = (_funcStub, configuredArgs.ToArray());
		}

		public static TypeSpec FillGeneric(TypeSpec source, TypeSpec[] fillers)
		{
			if (source.Type is GenericParamDesc generic) // если это сам дженерик параметр, то просто заменяем его filler'ом
			{
				if (generic.ParamIndex >= fillers.Length)
				{
					//какая-то хуйня произошла, передан список филлеров не покрывающий текущий параметр
					throw new NotImplementedException();
				}

				return fillers[generic.ParamIndex];
			}
			else if (source.ConfiguredGenericParams.Length > 0) // если это тип у которого установлены дженерик параметры, то каждый из них тоже прогоняем через эту функцию на случай если у него самого есть незаполненные дженерик параметры
			{
				var filledGenerics = source.ConfiguredGenericParams.Select(x => FillGeneric(x, fillers)).ToArray();
				return new TypeSpec(source.Type, source.Qualifiers, filledGenerics);
			}
			else if (source.Type.HasGenericParams) // если это дженерик тип и дженерик парамтеры не заполнены то тупо заполняем их массивом fillers
			{
				if (fillers.Length != source.Type.GenericParams.Count)
					throw new NotImplementedException();

				return new TypeSpec(source.Type, source.Qualifiers, fillers);
			}

			// это не дженерик
			return source;
		}

		/// <summary>
		/// Возвращает <see cref="ITypeRef"/> без учета квалифиакторов, только Type + установленные Generic параметры
		/// </summary>
		/// <param name="typeSpec"></param>
		/// <returns></returns>
		public ITypeRef GetTypeRef(TypeSpec typeSpec)
		{
			return _typesList[GetKey(typeSpec)];
		}

		public ITypeRef GetTypeRef(TypeSpec typeSpec, TypeSpec[]? genericFillers)
		{
			if (genericFillers != null)
			{
				typeSpec = FillGeneric(typeSpec, genericFillers);
			}

			return GetTypeRef(typeSpec);
		}

		/// <summary>
		/// Возвращает <see cref="ITypeRef"/> с учетом квалификаторов
		/// </summary>
		/// <param name="typeSpec"></param>
		/// <returns></returns>
		public ITypeRef GetRealTypeRef(TypeSpec typeSpec)
		{
			var baseType = GetTypeRef(typeSpec);
			if (!typeSpec.QualifiersExists) return baseType;

			if (typeSpec.Qualifiers.Length > 1)
				return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, typeSpec.Qualifiers.Skip(1), baseType);

			return typeSpec.Qualifiers[0].GetTypeRef(_typesProvider, [], baseType);
		}

		public ITypeRef GetRealTypeRef(TypeSpec typeSpec, TypeSpec[]? genericFillers)
		{
			if (genericFillers != null)
			{
				typeSpec = FillGeneric(typeSpec, genericFillers);
			}

			return GetRealTypeRef(typeSpec);
		}

		public IFuncRefData GetFunctionalBlockRef(IFunctionalBlockDesc desc, TypeSpec? parentType)
		{
			if (parentType != null)
			{
				var xer = _classMembersList[desc];
				return xer[GetKey(parentType.Value)].refData;
			}
			else
			{
				return _singleFunctions[desc];
			}
		}

		/// <summary>
		/// Возвращает все <see cref="IFuncRefData"/> для переданной функции. Если эта обычная функция, то значение будет только одно, 
		/// в случае generic функций будут <see cref="IFuncRefData"/> на каждую конфигурацию generic параметров
		/// </summary>
		/// <param name="desc"></param>
		/// <returns></returns>
		public List<(TypeSpec? implictArg, IReadOnlyCollection<TypeSpec> args, IFuncRefData refData)> GetAllFunctionRefs(IFunctionalBlockDesc desc)
		{
			if (_classMembersList.TryGetValue(desc, out var refs))
			{
				return refs.Select(x =>
				{
					TypeSpec? implictArg = desc.ImplicitArgument != null ? FillGeneric(desc.ImplicitArgument.Value, x.Key.ConfiguredGenericParams) : null;
					return (implictArg, (IReadOnlyCollection<TypeSpec>)x.Value.configuredArgs, x.Value.refData);
				}
				).ToList();
			}
			else
			{
				return new List<(TypeSpec? implictArg, IReadOnlyCollection<TypeSpec> args, IFuncRefData refData)>()
				{
					(null, desc.Arguments.Select(x => x.Declaring).ToArray(), _singleFunctions[desc])
				};
			}
		}

		public void BuildSigantures(ICodeGenerator codeGenerator)
		{
			foreach (var (type, typeRef) in _typesList.ToArray())
			{
				if ((typeRef is not TypeRefStub) || type.Type is GenericParamDesc) continue;
				CreateTypeBody(codeGenerator, type);
			}

			foreach (var func in _singleFunctions.Keys.ToArray())
			{
				var typesRefs = func.Arguments.Select(x => GetRealTypeRef(x.Declaring));
				var funcRef = codeGenerator.CreateFunctionSiganture(func.Name,
					GetRealTypeRef(func.ReturnType), typesRefs, func.IsInfArgs);

				_singleFunctions[func] = funcRef;
			}

			foreach ((var blockDesc, var refs) in _classMembersList)
			{
				var sourceArgs = blockDesc.Arguments;

				// TODO: залупа какая-то, код написан так будто ImplicitArgument обязательно является родителским классом. Хотя принадлежность ImplicitArgument к экземпляру родительского класса это зона ответственности Emiter. Надо отрефакторить
				if (blockDesc.ImplicitArgument == null || !blockDesc.ImplicitArgument.Value.Type.HasGenericParams)
				{
					var configuredArgs = refs.First();
					var typesRefs = new List<ITypeRef>();

					if (blockDesc.ImplicitArgument != null)
					{
						typesRefs.Add(GetRealTypeRef(blockDesc.ImplicitArgument.Value));
					}

					typesRefs.AddRange(sourceArgs.Select(x => GetRealTypeRef(x.Declaring)));

					var funcRef = codeGenerator.CreateFunctionSiganture(blockDesc.Name,
						GetRealTypeRef(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);

					_classMembersList[blockDesc][configuredArgs.Key] = (funcRef, sourceArgs.Select(x => x.Declaring).ToArray());
				}
				else
				{
					// TODO: написать дженерик где T будет использоваться только для возвращаемого типа и посмотреть где отъебнет
					foreach (var configuredParentType in refs.Keys)
					{
						var typesRefs = new List<ITypeRef>();

						// TODO: создание сигнатур вынести в отдельный класс, рядом с Emiter, чтобы он принимал просто опсиание функции
						if (blockDesc.ImplicitArgument != null)
						{
							var parent = blockDesc.ImplicitArgument.Value;
							typesRefs.Add(GetRealTypeRef(new TypeSpec(parent.Type, parent.Qualifiers, configuredParentType.ConfiguredGenericParams)));
						}

						string genericEndingName = "__";

						var configuredArgs = new List<TypeSpec>();

						for (int i = 0; i < sourceArgs.Length; i++)
						{
							var typeSpec = sourceArgs[i].Declaring;
							if (typeSpec.Type is not GenericParamDesc generic)
							{
								typesRefs.Add(GetRealTypeRef(typeSpec));
								configuredArgs.Add(typeSpec);
							}
							else
							{
								genericEndingName = $"{genericEndingName}gp{i}{typeSpec.ToString("_")}__";
								TypeSpec currentConfiguedParameter = configuredParentType.ConfiguredGenericParams[generic.ParamIndex];
								typesRefs.Add(GetRealTypeRef(currentConfiguedParameter));
								configuredArgs.Add(currentConfiguedParameter);
							}
						}

						var funcRef = codeGenerator.CreateFunctionSiganture($"{blockDesc.Name}{genericEndingName}",
							GetRealTypeRef(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);

						var sourceData = _classMembersList[blockDesc][configuredParentType];
						_classMembersList[blockDesc][configuredParentType] = (funcRef, configuredArgs.ToArray());
					}
				}

			}
		}

		private ITypeRef CreateTypeBody(ICodeGenerator codeGenerator, TypeSpec typeSpec)
		{
			ITypeRef typeRef;
			if (!typeSpec.Type.HasGenericParams)
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

				fieldsTypesRefs.Add(GetRealTypeRef(fieldDeclaring));
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
