using EvolZero.Core;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Analysis.Semantic
{
	internal class AccessAnalyzer : SemanticTreeVisitor<object?>
	{
		public const string ACCESS_LAYER = "AccessModifiersAnalyzer";

		private readonly ErrorsBag _errorsBag;

		private TypeDesc? _currentClass = null;

		public AccessAnalyzer(ErrorsBag errorsBag)
		{
			_errorsBag = errorsBag;
		}

		protected override void HandleClass(ClassStatement statement)
		{
			TypeDesc? previosClass = _currentClass;
			_currentClass = statement.TypeDesc;

			base.HandleClass(statement);

			_currentClass = previosClass;
		}

		protected override object? StructureFiledAccess(StructureFieldAccessExpression expr)
		{
			HandleExpression(expr.StructureGetting);

			if (expr.Field.Access == AccessModifier.Private && _currentClass != expr.StructureGetting.ResultTypeSpec.Type)
			{
				_errorsBag.AddError(ACCESS_LAYER, "ACC001",
					$"The field '{expr.Field.Name}' of class '{expr.StructureGetting.ResultTypeSpec.Type.Name}' is private and cannot be accessed from this context",
					expr.Pos);
			}

			return null;
		}

		protected override object? CallFunction(CallFunctionExpression expr)
		{
			foreach (var arg in expr.Arguments)
			{
				HandleExpression(arg);
			}

			if (expr.Function.Access == AccessModifier.Private && expr.Function.DeclaringType != null && _currentClass != expr.Function.DeclaringType)
			{
				_errorsBag.AddError(ACCESS_LAYER, "ACC002",
					$"The method '{expr.Function.Name}' of class '{expr.Function.DeclaringType.Name}' is private and cannot be called from this context",
					expr.Pos);
			}

			return null;
		}

		protected override object? CallConstructor(CallConstructorExpression expr)
		{
			HandleExpression(expr.MemoryGetting);
			foreach (var arg in expr.Arguments)
			{
				HandleExpression(arg);
			}

			TypeDesc constructedType = expr.MemoryGetting.ResultTypeSpec.Type;

			if (expr.Constructor.Access == AccessModifier.Private && _currentClass != constructedType)
			{
				_errorsBag.AddError(ACCESS_LAYER, "ACC003",
					$"The constructor of class '{constructedType.Name}' is private and cannot be invoked from this context",
					expr.Pos);
			}

			return null;
		}
	}
}
