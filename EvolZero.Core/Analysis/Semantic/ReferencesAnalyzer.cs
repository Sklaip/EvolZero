using EvolZero.Core;
using EvolZero.Core.LogicModels;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.Analysis.Semantic
{
	public class ReferencesData
	{
		public string? VariableName;
		public bool IsOwnerRef = false;
		public bool IsBorrowRef = false;

		public ReferencesData(bool isBorrowRef, bool isOwnerRef, string? variableName)
		{
			IsBorrowRef = isBorrowRef;
			IsOwnerRef = isOwnerRef;
			VariableName = variableName;
		}

		public ReferencesData(Expression expr):
			this(expr.ResultTypeSpec.IsBorrowRef, expr.ResultTypeSpec.IsOwnerRef, null)
		{
		}
	}

	public class ReferencesAnalyzer : SemanticTreeVisitor<ReferencesData?>
	{
		public const string REFERENCES_LAYER = "ReferencesAnalyzer";

		private readonly ErrorsBag _errorsBag;
		private HashSet<string> _givenRefs = new HashSet<string>();

		public ReferencesAnalyzer(ErrorsBag errorsBag)
		{
			_errorsBag = errorsBag;
		}

		protected override void HandleFunctionalBlock<TBlock>(TBlock statement)
		{
			base.HandleFunctionalBlock(statement);
			_givenRefs = new HashSet<string>();
		}

		protected override ReferencesData? CallFunction(CallFunctionExpression expr)
		{
			for (int i = 0; i < expr.Function.Arguments.Length; i++)
			{
				TypeSpec acceptedtype = expr.Function.Arguments[i].Declaring;
				Expression argument = expr.Arguments[i];

				var res = HandleExpression(argument);
				if (acceptedtype.IsOwnerRef)
				{
					if (argument.ResultTypeSpec.IsBorrowRef)
					{
						_errorsBag.AddError(REFERENCES_LAYER, "REF001", "Cannot pass a borrow reference as an owner reference argument", expr.Pos);
					}

					if (res?.VariableName != null)
					{
						_givenRefs.Add(res.VariableName);
					}
				}
			}

			return new ReferencesData(expr);
		}

		protected override ReferencesData VarAccess(VariableAccessExpression expr)
		{
			if (_givenRefs.Contains(expr.Name))
			{
				_errorsBag.AddError(REFERENCES_LAYER, "REF002", $"The variable '{expr.Name}' was already moved and cannot be used", expr.Pos);
			}

			return new ReferencesData(expr.ResultTypeSpec.IsBorrowRef, expr.ResultTypeSpec.IsOwnerRef, expr.Name);
		}
	}
}
