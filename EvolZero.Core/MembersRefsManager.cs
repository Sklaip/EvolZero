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

		private Dictionary<IFunctionalBlockDesc, Dictionary<FuncArgs, (IFuncRefData refData, TypeSpec[] configuredArgs)>> _functionalBlocksList = new();

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
				if (type.IsGeneric) continue;
				_typesList.TryAdd(new TypeSpec(type), _typeStub);

				AddFunctions(type.Functions.Values.SelectMany(x => x));
				AddFunctions(type.Constructors);
				AddFunctions(type.Destructors);
			}

			AddFunctions(table.Functions.Values.SelectMany(x => x));
		}

		private void AddFunctions(IEnumerable<IFunctionalBlockDesc> functions)
		{
			foreach (var func in functions)
			{
				var args = func.Arguments.Select(x => GetKey(x.Declaring)).ToArray();
				var configuredArgs = func.Arguments.Select(x => x.Declaring).ToArray();

				_functionalBlocksList[func] = new Dictionary<FuncArgs, (IFuncRefData refData, TypeSpec[] configuredArgs)>()
				{
					[new FuncArgs(args)] = (_funcStub, configuredArgs)
				};
			}
		}

		public void AddTypeIfGeneric(TypeSpec typeSpec)
		{
			if (typeSpec.ConfiguredGenericParams.Length > 0)
				_typesList.TryAdd(GetKey(typeSpec), _typeStub);
		}

		public void AddFuncIfGeneric(IFunctionalBlockDesc desc, IEnumerable<TypeSpec> arguments)
		{
			if (!arguments.Any(x => x.ConfiguredGenericParams.Length > 0)) return;

			if (!_functionalBlocksList.TryGetValue(desc, out Dictionary<FuncArgs, (IFuncRefData refData, TypeSpec[] configuredArgs)>? funcRefs))
			{
				funcRefs = [];
				_functionalBlocksList[desc] = funcRefs;
			}

			funcRefs[new FuncArgs(arguments.Select(GetKey).ToArray())] = (_funcStub, arguments.ToArray());
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

		public IFuncRefData GetFunctionalBlockRef(IFunctionalBlockDesc desc, IEnumerable<TypeSpec> arguments)
		{
			var args = arguments.Select(GetKey).ToArray();
			return _functionalBlocksList[desc][new FuncArgs(args)].refData;
		}

		/// <summary>
		/// Возвращает все <see cref="IFuncRefData"/> для переданной функции. Если эта обычная функция, то значение будет только одно, 
		/// в случае generic функций будут <see cref="IFuncRefData"/> на каждую конфигурацию generic параметров
		/// </summary>
		/// <param name="desc"></param>
		/// <returns></returns>
		public List<(IReadOnlyCollection<TypeSpec> args, IFuncRefData refData)> GetAllFunctionRefs(IFunctionalBlockDesc desc)
		{
			var refs = _functionalBlocksList[desc];
			return refs.Select(x => ((IReadOnlyCollection<TypeSpec>)x.Value.configuredArgs, x.Value.refData)).ToList();
		}

		public void BuildSigantures(ICodeGenerator codeGenerator)
		{
			foreach (var (type, typeRef) in _typesList.ToArray())
			{
				CreateTypeBody(codeGenerator, type);
			}

			foreach ((var blockDesc, var refs) in _functionalBlocksList)
			{
				var args = blockDesc.Arguments;

				if (refs.Count < 2) // в списке только один элемент, значит это не дженерик функция
				{
					var configuredArgs = refs.First(); // специально делаем first, чтобы если что-то пошло не так и было 0 элементов в refs, то тут выбило исключение, а не хрен пойми что произошло потом

					var typesRefs = args.Select(x => GetRealTypeRef(x.Declaring));
					var funcRef = codeGenerator.CreateFunctionSiganture(blockDesc.Name, 
						GetRealTypeRef(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);

					_functionalBlocksList[blockDesc][configuredArgs.Key] = (funcRef, args.Select(x => x.Declaring).ToArray());
				}
				else
				{
					// TODO: написать дженерик где T будет использоваться только для возвращаемого типа и посмотреть где отъебнет
					foreach (var configuredArgs in refs.Keys)
					{
						var typesRefs = new List<ITypeRef>();

						string genericEndingName = "__";

						for (int i = 0; i < args.Length; i++)
						{
							var passedTypeSpec = args[i].Declaring;
							if (!passedTypeSpec.Type.IsGeneric)
							{
								typesRefs.Add(GetRealTypeRef(passedTypeSpec));
							}
							else
							{
								genericEndingName = $"{genericEndingName}gp{i}{passedTypeSpec.ToString("_")}__";
								typesRefs.Add(GetRealTypeRef(configuredArgs.Args[i]));
							}
						}

						var funcRef = codeGenerator.CreateFunctionSiganture($"{blockDesc.Name}{genericEndingName}", GetRealTypeRef(blockDesc.ReturnType), typesRefs, blockDesc.IsInfArgs);

						var sourceData = _functionalBlocksList[blockDesc][configuredArgs];
						_functionalBlocksList[blockDesc][configuredArgs] = (funcRef, sourceData.configuredArgs);
					}
				}

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
