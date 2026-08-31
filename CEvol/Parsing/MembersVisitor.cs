using Antlr4.Runtime.Misc;
using EvolZero.Core;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation;
using EvolZero.Parsing.Models;
using static CEvolParser;
using static EvolZero.Core.MemebersModels.Qualifier;

namespace EvolZero.Parsing
{
	internal class MembersVisitor : CEvolParserBaseVisitor<object?>
	{
		public string CurrentNameSpace { get; private set; } = null!;
		public Dictionary<string, ClassSignature> Classes = new();
		public Dictionary<string, List<FuncSignature>> SingleFunctions = new();
		public HashSet<string> Usings = new();

		private Dictionary<string, VariableSignature>? _currentClassVariables = null;
		private Dictionary<string, List<FuncSignature>>? _currentClassFunctions = null;
		private List<ConstructorSignature>? _currentClassConstructors = null;

		public override object? VisitNamespaceDecl([NotNull] CEvolParser.NamespaceDeclContext context)
		{
			CurrentNameSpace = context.IDENTIFIER().GetText();
			if (CurrentNameSpace == null)
				throw new NotImplementedException();

			return VisitChildren(context);
		}

		public override object? VisitUsingDecl([NotNull] CEvolParser.UsingDeclContext context)
		{
			string name = context.IDENTIFIER().GetText();
			if (name == null) throw new NotImplementedException();

			Usings.Add(name);
			return VisitChildren(context);
		}

		public override object? VisitClassDecl([NotNull] CEvolParser.ClassDeclContext context)
		{
			_currentClassVariables = new();
			_currentClassFunctions = new();
			_currentClassConstructors = new();

			var typeName = context.IDENTIFIER().ToString();
			var fullTypeName = $"{CurrentNameSpace}.{typeName}";
			if (typeName == null || Classes.ContainsKey(fullTypeName))
				throw new NotImplementedException();

			foreach (var fieldDecl in context.fieldDecl())
			{
				Visit(fieldDecl);
			}

			foreach (var funcDecl in context.functionDecl())
			{
				Visit(funcDecl);
			}

			foreach (var funcDecl in context.constructorDecl())
			{
				Visit(funcDecl);
			}

			var currentClassDesc = new ClassSignature(fullTypeName, _currentClassConstructors, _currentClassFunctions, _currentClassVariables);
			Classes[fullTypeName] = currentClassDesc;

			_currentClassVariables = null;
			_currentClassFunctions = null;
			_currentClassConstructors = null;

			return null;
		}

		public override object? VisitFieldDecl([NotNull] CEvolParser.FieldDeclContext context)
		{
			var fieldName = context.IDENTIFIER().ToString();
			if (fieldName == null || _currentClassVariables.ContainsKey(fieldName))
				throw new NotImplementedException();

			var fieldSpec = ParseTypeSpec(context.typeSpec());

			AccessModifier access = ParseAccessModifier(context.accessModifier(), isClassMember: true);

			var desc = new VariableSignature(fieldName, fieldSpec, access);
			_currentClassVariables.Add(fieldName, desc);

			return null;
		}

		public override object VisitFunctionDecl([NotNull] CEvolParser.FunctionDeclContext context)
		{
			var prms = context.@params();

			List<(TypeDeclaring Type, string Name)>? parameters = null;

			if (prms != null)
			{
				parameters = ParseParams(prms);
			}

			TypeDeclaring typeSpec = ParseTypeSpec(context.typeSpec());
			string? funcName = context.IDENTIFIER().ToString();
			if (funcName == null) throw new NotImplementedException();

			var funcsList = _currentClassFunctions ?? SingleFunctions;

			AccessModifier access = ParseAccessModifier(context.accessModifier(), _currentClassFunctions != null);

			if (!funcsList.TryGetValue(funcName, out List<FuncSignature>? functions))
			{
				functions = new();
				funcsList[funcName] = functions;
			}

			functions.Add(new FuncSignature(funcName, typeSpec, parameters, [], access));

			return null;
		}

		public override object VisitAbstractFunctionDecl([NotNull] CEvolParser.AbstractFunctionDeclContext context)
		{
			var prms = context.@params();

			List<(TypeDeclaring Type, string Name)>? parameters = null;

			if (prms != null)
			{
				parameters = ParseParams(prms);
			}

			TypeDeclaring typeSpec = ParseTypeSpec(context.typeSpec());
			string? funcName = context.IDENTIFIER().ToString();
			if (funcName == null) throw new NotImplementedException();

			string[] modifers = context.extraModifier()?.Select(x => x?.GetText() ?? "").ToArray() ?? [];

			if (!modifers.Contains("extern")) throw new NotImplementedException();

			var funcsList = _currentClassFunctions ?? SingleFunctions;

			AccessModifier access = ParseAccessModifier(context.accessModifier(), _currentClassFunctions != null);

			if (!funcsList.TryGetValue(funcName, out List<FuncSignature>? functions))
			{
				functions = new();
				funcsList[funcName] = functions;
			}

			functions.Add(new FuncSignature(funcName, typeSpec, parameters, modifers, access));

			return null;
		}

		public override object VisitConstructorDecl([NotNull] CEvolParser.ConstructorDeclContext context)
		{
			var prms = context.@params();

			List<(TypeDeclaring Type, string Name)>? parameters = null;

			if (prms != null)
			{
				parameters = ParseParams(prms);
			}

			if (_currentClassConstructors == null) throw new NotImplementedException();

			AccessModifier access = ParseAccessModifier(context.accessModifier(), isClassMember: true);

			_currentClassConstructors.Add(new ConstructorSignature(parameters, [], access));

			return null;
		}

		private AccessModifier ParseAccessModifier(CEvolParser.AccessModifierContext? context, bool isClassMember)
		{
			if (context == null)
			{
				return isClassMember ? AccessModifier.Private : AccessModifier.Public;
			}

			return context.GetText() switch
			{
				"public" => AccessModifier.Public,
				"private" => AccessModifier.Private,
				_ => throw new NotImplementedException()
			};
		}
		private List<(TypeDeclaring Type, string Name)> ParseParams([NotNull] CEvolParser.ParamsContext context)
		{
			var parameters = new List<(TypeDeclaring Type, string Name)>();

			int count = context.typeSpec().Length;

			for (int i = 0; i < count; i++)
			{
				TypeDeclaring paramDecl = ParseTypeSpec(context.typeSpec(i));
				string paramName = context.IDENTIFIER(i).GetText();

				parameters.Add((paramDecl, paramName));
			}

			return parameters;
		}

		private TypeDeclaring ParseTypeSpec([NotNull] CEvolParser.TypeSpecContext context)
		{
			var typeName = context.IDENTIFIER().GetText();
			if (string.IsNullOrEmpty(typeName))
				throw new NotImplementedException();

			var qualifiers = new List<QualifierWorkpiece>();
			foreach (var qualifier in context.qualifier())
			{
				qualifiers.Add(new QualifierWorkpiece() { Kind = qualifier.GetText() });
			}

			foreach (var arr in context.arraySpec())
			{
				qualifiers.Add(ParseArraySpec(arr));
			}

			return new TypeDeclaring(typeName, qualifiers.ToArray(), []);
		}

		public QualifierWorkpiece ParseArraySpec([NotNull] CEvolParser.ArraySpecContext context)
		{
			var numExpr = context.expression() as NumberExprContext;

			if (numExpr == null) throw new NotImplementedException();

			var value = numExpr.NUMBER().GetText();
			var num = ulong.Parse(value);

			return new QualifierWorkpiece()
			{
				Kind = "array",
				ArraySize = num
			};
		}

	}
}
