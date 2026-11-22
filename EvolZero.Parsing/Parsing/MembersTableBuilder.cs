using EvolZero.Core;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation;
using EvolZero.Parsing.Models;

namespace EvolZero.Parsing
{
	public class MembersTableBuilder
	{
		private string _currentNameSpace { get; }
		private HashSet<string> _usings { get; }
		private Dictionary<string, ClassSignature> _classes = new();
		private Dictionary<string, List<FuncSignature>> _singleFunctions = new();
		private readonly ICodeGenerator _codeGenerator;
		private Dictionary<string, TypeDesc> _parsedClasses = new();
		private Dictionary<string, FuncDesc[]> _parsedSingleFunctions = new Dictionary<string, FuncDesc[]>();

		private IReadOnlyDictionary<string, TypeDesc> _existsClasses = null!;
		private IPlatformTypesProvider _typesProvider;

		public MembersTableBuilder(string currentNamespcae, HashSet<string> usings, Dictionary<string, ClassSignature> classes,
			Dictionary<string, List<FuncSignature>> singleFunctions, ICodeGenerator codeGenerator, IPlatformTypesProvider typesProvider)
		{
			_currentNameSpace = currentNamespcae;
			_usings = usings;
			_classes = classes;
			_singleFunctions = singleFunctions;
			_codeGenerator = codeGenerator;
			_typesProvider = typesProvider;
		}

		private TypeDesc FindTypeForDeclaring(TypeDeclaring typeDecl, string? currentClassName)
		{
			var typeDesc = FindTypeDesc(typeDecl.TypeName);

			if (typeDesc == null && currentClassName != null)
			{
				typeDesc = FindTypeDesc($"{currentClassName}${typeDecl.TypeName}");
			}

			// этого типа не существует
			if (typeDesc == null) 
				throw new NotImplementedException();

			return typeDesc;
		}

		private TypeDesc? FindTypeDesc(string name)
		{
			if (!_parsedClasses.TryGetValue(name, out TypeDesc? type)
				&& !_parsedClasses.TryGetValue($"{_currentNameSpace}.{name}", out type))
			{
				if (_existsClasses.TryGetValue($"{name}", out type)) return type;

				foreach (var use in _usings)
				{
					if (_existsClasses.TryGetValue($"{use}.{name}", out type)) return type;
				}

				// этого типа не существует
				return null;
			}

			return type;
		}

		private void ConstructorsAnalyze(List<ConstructorSignature> constructorsList, ICodeGenerator codeGenerator, TypeDesc currentClass)
		{
			foreach (var ctor in constructorsList)
			{
				var arguments = new List<Argument>();
				var agrumentsRefs = new List<ITypeRef>();

				string funcName = $"{currentClass.Name}_ctor";
				agrumentsRefs.Add(codeGenerator.PointerType);

				if (ctor.Arguments != null)
				{
					foreach ((TypeDeclaring Type, string Name) funcArgument in ctor.Arguments)
					{
						TypeDesc argumentType = FindTypeForDeclaring(funcArgument.Type, currentClass.Name);
						var declaring = new TypeSpec(argumentType, QualifierWorkpiece.ToQualifiers(funcArgument.Type.Qualifiers));
						arguments.Add(new Argument(declaring, funcArgument.Name));
						agrumentsRefs.Add(declaring.QualifiersExists ? codeGenerator.PointerType : argumentType.TypeRef);
					}
				}

				IFuncRefData funcRefs;
				bool infArgs = ctor.modifiers.Contains("infargs"); // TODO: енумом модификаторы сделать что ли, или флагами
				funcRefs = codeGenerator.CreateFunctionSiganture(funcName, codeGenerator.VoidType, agrumentsRefs, infArgs);

				LifetimeDecl[] lifetimes;
				if (ctor.Lifetimes == null)
				{
					lifetimes = arguments.Where(x => x.Declaring.IsOwnerRef).Select(x =>
						new LifetimeDecl(
							new LifetimeDecl.Key(LifetimeDecl.KeyType.Var, x.Name),
							new LifetimeDecl.Key(LifetimeDecl.KeyType.This, null)
						)).ToArray();
				}
				else
				{
					lifetimes = ctor.Lifetimes.ToArray();
				}

				currentClass.Constructors.Add(new ConstructorDesc(arguments.ToArray(), funcRefs, ctor.Access, lifetimes));
			}
		}

		private void DestructorsAnalyze(List<DestructorSignature> destructorsList, ICodeGenerator codeGenerator, TypeDesc currentClass)
		{
			foreach (var dtor in destructorsList)
			{
				string funcName = $"{currentClass.Name}_dtor";

				IFuncRefData funcRefs;
				funcRefs = codeGenerator.CreateFunctionSiganture(funcName, codeGenerator.VoidType, [codeGenerator.PointerType], false);

				currentClass.Destructors.Add(new DestructorDesc(funcRefs, dtor.Access));
			}
		}

		private void FunctionsAnalyze(Dictionary<string, List<FuncSignature>> rawFunctionsList,
			Dictionary<string, FuncDesc[]> listToAdd, TypeDesc? currentClass = null)
		{
			foreach (var funcsKey in rawFunctionsList.Keys)
			{
				var funcList = new List<FuncDesc>();

				foreach (var func in rawFunctionsList[funcsKey])
				{
					TypeDesc returnType = FindTypeForDeclaring(func.ReturnType, currentClass?.Name);
					var returnTypeQualifers = QualifierWorkpiece.ToQualifiers(func.ReturnType.Qualifiers);

					var arguments = new List<Argument>();
					var agrumentsRefs = new List<ITypeRef>();

					string funcName;
					if (currentClass != null)
					{
						funcName = $"{currentClass.Name}_{func.Name}"; // TODO: если неколько функций с одним именем, то это в названии надо учитывать
						agrumentsRefs.Add(_codeGenerator.PointerType);
					}
					else
					{
						funcName = $"{func.Name}";
					}

					if (func.Arguments != null)
					{
						foreach ((TypeDeclaring Type, string Name) funcArgument in func.Arguments)
						{
							TypeDesc argumentType = FindTypeForDeclaring(funcArgument.Type, currentClass?.Name);
							var declaring = new TypeSpec(argumentType, QualifierWorkpiece.ToQualifiers(funcArgument.Type.Qualifiers));
							arguments.Add(new Argument(declaring, funcArgument.Name));
							agrumentsRefs.Add(declaring.QualifiersExists ? _codeGenerator.PointerType : argumentType.TypeRef);
						}
					}

					IFuncRefData funcRefs;
					bool infArgs = func.modifiers.Contains("infargs"); // TODO: енумом модификаторы сделать что ли, или флагами
					var resutlTypeSpec = new TypeSpec(returnType, returnTypeQualifers);
					var rusltTypeRef = resutlTypeSpec.GetRealTypeRef(_typesProvider);

					funcRefs = _codeGenerator.CreateFunctionSiganture(funcName, rusltTypeRef, agrumentsRefs, infArgs);

					var funcDesc = new FuncDesc(resutlTypeSpec, func.Name, arguments.ToArray(), funcRefs,
						infArgs, func.Access, func.Lifetimes?.ToArray() ?? [], currentClass);
					funcList.Add(funcDesc);
				}

				// TODO: сделать проверку на дубликаты методов
				listToAdd.Add(funcsKey, funcList.ToArray());
			}
		}

		public void PrepareTable(Dictionary<string, TypeDesc> generalTypesList)
		{
			_parsedClasses = new Dictionary<string, TypeDesc>();

			foreach (var currentClass in _classes.Values)
			{
				if (generalTypesList.ContainsKey(currentClass.Name))
				{
					//такой класс уже существует
					throw new NotImplementedException();
				}

				var classStructure = _codeGenerator.CreateStructure(currentClass.Name);
				var classDesc = new TypeDesc(currentClass.Name, classStructure, [], [], [], []);

				_parsedClasses.Add(currentClass.Name, classDesc);
				generalTypesList.Add(currentClass.Name, classDesc);

				for (int i = 0; i < currentClass.GenericParams.Count; i++)
				{
					string? param = currentClass.GenericParams[i];
					var genericParamName = $"{currentClass.Name}${param}";
					var genericParam = new GenericParamDesc(genericParamName, null!, [], [], [], [], param, i);

					_parsedClasses.Add(genericParamName, genericParam);
					generalTypesList.Add(genericParamName, genericParam);
				}
			}

			_existsClasses = generalTypesList;
		}

		private void BuildClassesList()
		{
			foreach (var currentClass in _classes.Values)
			{
				var currentClassTypeDesc = _parsedClasses[currentClass.Name];

				var filedTypes = new List<ITypeRef>();
				uint fieldNum = 0;
				foreach (var field in currentClass.Fields.Values)
				{
					TypeDesc fieldType = FindTypeForDeclaring(field.Type, currentClass.Name);
					var qualifers = QualifierWorkpiece.ToQualifiers(field.Type.Qualifiers);

					//if (!fieldType.IsBaseType && (qualifers == null || qualifers.Length < 1)) throw new NotImplementedException(); // TODO: сделать возможность пихать класс в класс по значению
					var typeSpec = new TypeSpec(fieldType, qualifers);
					currentClassTypeDesc.Variables.Add(field.Name, new VariableDesc(typeSpec, field.Name, fieldNum, field.Access));

					filedTypes.Add(typeSpec.GetRealTypeRef(_typesProvider));

					fieldNum++;
				}

				_codeGenerator.FillStructureBody(currentClassTypeDesc.TypeRef, filedTypes);

				if (currentClass.Ctors.Count == 0) currentClass.Ctors.Add(new ConstructorSignature(null, [], AccessModifier.Public, []));
				if (currentClass.Dtors.Count == 0) currentClass.Dtors.Add(new DestructorSignature([], AccessModifier.Public));

				FunctionsAnalyze(currentClass.Functions, currentClassTypeDesc.Functions, currentClassTypeDesc);
				ConstructorsAnalyze(currentClass.Ctors, _codeGenerator, currentClassTypeDesc);
				DestructorsAnalyze(currentClass.Dtors, _codeGenerator, currentClassTypeDesc);
			}
		}

		public MembersTable Build()
		{
			BuildClassesList();
			FunctionsAnalyze(_singleFunctions, _parsedSingleFunctions);

			return new MembersTable(_parsedSingleFunctions, _parsedClasses);
		}
	}
}
