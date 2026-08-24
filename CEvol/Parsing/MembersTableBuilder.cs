using EvolZero.Core;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation;
using EvolZero.Parsing.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static EvolZero.Core.MemebersModels.Qualifier;

namespace EvolZero.Parsing
{
	internal class MembersTableBuilder
	{
		private string _currentNameSpace { get; }
		private HashSet<string> _usings { get; }
		private Dictionary<string, ClassSignature> _classes = new();
		private Dictionary<string, List<FuncSignature>> _singleFunctions = new();
		private readonly CodeGenerator _codeGenerator;
		private Dictionary<string, TypeDesc> _parsedClasses = new();
		private Dictionary<string, FuncDesc[]> _parsedSingleFunctions = new Dictionary<string, FuncDesc[]>();

		private IReadOnlyDictionary<string, TypeDesc> _existsClasses = null!;

		public MembersTableBuilder(string currentNamespcae, HashSet<string> usings, Dictionary<string, ClassSignature> classes, Dictionary<string, List<FuncSignature>> singleFunctions, CodeGenerator codeGenerator)
		{
			_currentNameSpace = currentNamespcae;
			_usings = usings;
			_classes = classes;
			_singleFunctions = singleFunctions;
			_codeGenerator = codeGenerator;
		}

		private TypeDesc FindTypeForDeclaring(TypeDeclaring typeDecl)
		{
			if (!_parsedClasses.TryGetValue(typeDecl.TypeName, out TypeDesc? type) && !_parsedClasses.TryGetValue($"{_currentNameSpace}.{typeDecl.TypeName}", out type))
			{
				if (_existsClasses.TryGetValue($"{typeDecl.TypeName}", out type)) return type;

				foreach (var use in _usings)
				{
					if (_existsClasses.TryGetValue($"{use}.{typeDecl.TypeName}", out type)) return type;
				}

				// этого типа не существует
				throw new NotImplementedException();
			}

			return type;
		}

		private void ConstructorsAnalyze(List<ConstructorSignature> constructorsList, CodeGenerator codeGenerator, TypeDesc currentClass)
		{
			foreach (var ctor in constructorsList)
			{
				var arguments = new List<Argument>();
				var agrumentsRefs = new List<TypeRef>();

				string funcName = $"{currentClass.Name}_ctor";
				agrumentsRefs.Add(codeGenerator.PointerType);

				if (ctor.Arguments != null)
				{
					foreach ((TypeDeclaring Type, string Name) funcArgument in ctor.Arguments)
					{
						TypeDesc argumentType = FindTypeForDeclaring(funcArgument.Type);
						var declaring = new TypeSpec(argumentType, Qualifier.FromString(funcArgument.Type.Qualifiers));
						arguments.Add(new Argument(declaring, funcArgument.Name));
						agrumentsRefs.Add(declaring.QualifiersExists ? codeGenerator.PointerType : argumentType.TypeRef);
					}
				}

				FuncRefData funcRefs;
				bool infArgs = ctor.modifiers.Contains("infargs"); // TODO: енумом модификаторы сделать что ли, или флагами
				funcRefs = codeGenerator.CreateFunctionSiganture(funcName, codeGenerator.VoidType, agrumentsRefs, infArgs);

				currentClass.Constructors.Add(new ConstructorDesc(arguments.ToArray(), funcRefs, ctor.Access));
			}
		}

		private void FunctionsAnalyze(Dictionary<string, List<FuncSignature>> rawFunctionsList,  Dictionary<string, FuncDesc[]> listToAdd, TypeDesc? currentClass = null)
		{
			foreach (var funcsKey in rawFunctionsList.Keys)
			{
				var funcList = new List<FuncDesc>();

				foreach (var func in rawFunctionsList[funcsKey])
				{
					TypeDesc returnType = FindTypeForDeclaring(func.ReturnType);
					var returnTypeQualifers = Qualifier.FromString(func.ReturnType.Qualifiers);

					var arguments = new List<Argument>();
					var agrumentsRefs = new List<TypeRef>();

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
							TypeDesc argumentType = FindTypeForDeclaring(funcArgument.Type);
							var declaring = new TypeSpec(argumentType, Qualifier.FromString(funcArgument.Type.Qualifiers));
							arguments.Add(new Argument(declaring, funcArgument.Name));
							agrumentsRefs.Add(declaring.QualifiersExists ? _codeGenerator.PointerType : argumentType.TypeRef);
						}
					}

					FuncRefData funcRefs;
					bool infArgs = func.modifiers.Contains("infargs"); // TODO: енумом модификаторы сделать что ли, или флагами
					if (func.ReturnType.Qualifiers == null || func.ReturnType.Qualifiers.Length < 1)
					{
						funcRefs = _codeGenerator.CreateFunctionSiganture(funcName, returnType.TypeRef, agrumentsRefs, infArgs);
					}
					else
					{
						funcRefs = _codeGenerator.CreateFunctionSiganture(funcName, QKindToTypeRef(returnTypeQualifers[0].Kind, _codeGenerator), agrumentsRefs, infArgs);
					}

					var funcDesc = new FuncDesc(new TypeSpec(returnType, returnTypeQualifers), func.Name, arguments.ToArray(), funcRefs, infArgs, func.Access, currentClass);
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

				var classDesc = new TypeDesc(currentClass.Name, classStructure, [], [], []);
				_parsedClasses.Add(currentClass.Name, classDesc);
				generalTypesList.Add(currentClass.Name, classDesc);
			}

			_existsClasses = generalTypesList;
		}

		private void BuildClassesList()
		{
			foreach (var currentClass in _classes.Values)
			{
				var currentClassTypeDesc = _parsedClasses[currentClass.Name];
				var filedTypes = new List<TypeRef>();
				uint fieldNum = 0;
				foreach (var field in currentClass.Fields.Values)
				{
					TypeDesc fieldType = FindTypeForDeclaring(field.Type);
					var qualifers = Qualifier.FromString(field.Type.Qualifiers);

					if (!fieldType.IsBaseType && (qualifers == null || qualifers.Length < 1)) throw new NotImplementedException(); // TODO: сделать возможность пихать класс в класс по значению

					currentClassTypeDesc.Variables.Add(field.Name, new VariableDesc(new TypeSpec(fieldType, qualifers), field.Name, fieldNum, field.Access));

					if (qualifers == null || qualifers.Length < 1)
					{
						filedTypes.Add(fieldType.TypeRef);
					}
					else
					{
						filedTypes.Add(QKindToTypeRef(qualifers[0].Kind, _codeGenerator));
					}

					fieldNum++;
				}

				_codeGenerator.FillStructureBody(currentClassTypeDesc.TypeRef, filedTypes);

				FunctionsAnalyze(currentClass.Functions, currentClassTypeDesc.Functions, currentClassTypeDesc);
				ConstructorsAnalyze(currentClass.Ctors, _codeGenerator, currentClassTypeDesc);
			}
		}

		// TODO: это куда-то вынести, код дублирует с SemanticAnalyzer
		private TypeRef QKindToTypeRef(QKind qKind, CodeGenerator codeGenerator)
		{
			switch (qKind)
			{
				case QKind.Reference:
				case QKind.Array:
				case QKind.BorrowReference:
					return codeGenerator.PointerType;
				default:
					throw new NotImplementedException();
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
