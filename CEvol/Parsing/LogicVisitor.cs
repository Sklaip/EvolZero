using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using EvolZero.Core;
using EvolZero.Core.Analysis;
using EvolZero.Core.Analysis.Semantic;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using System.Numerics;


namespace EvolZero.Parsing
{
	internal class LogicVisitor : CEvolParserBaseVisitor<Expression?>
	{
		private readonly MembersFinder _membersFinder;
		private readonly string _currentFile;
		private readonly SemanticTreeBuilder _semanticAnalyzer;
		private readonly TypeAnalyzer _typeAnalyzer;

		public Statement ResultStatement { get; private set; }

		public LogicVisitor(MembersFinder membersFinder, ErrorsBag errorsBag, string currentFile)
		{
			_typeAnalyzer = new TypeAnalyzer(membersFinder);
			_semanticAnalyzer = new SemanticTreeBuilder(membersFinder, _typeAnalyzer, errorsBag);
			_membersFinder = membersFinder;
			_currentFile = currentFile;
		}

		public override Expression? VisitProgram(CEvolParser.ProgramContext context)
		{
			_membersFinder.ClearUsings();

			VisitChildren(context);
			ResultStatement = _semanticAnalyzer.ExitFromBlock();

			return null;
		}

		public override Expression? VisitNamespaceDecl([NotNull] CEvolParser.NamespaceDeclContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			string name = context.IDENTIFIER().GetText();
			if (name == null) throw new NotImplementedException();

			_semanticAnalyzer.EnterToNameSpace(name);

			VisitChildren(context);

			_semanticAnalyzer.CurrentPosition = lastPos;

			return null;
		}

		public override Expression? VisitUsingDecl([NotNull] CEvolParser.UsingDeclContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			string name = context.IDENTIFIER().GetText();
			if (name == null) throw new NotImplementedException();

			_semanticAnalyzer.Using(name);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return null;
		}

		public override Expression? VisitClassDecl([NotNull] CEvolParser.ClassDeclContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			_semanticAnalyzer.EnterToClass(context.IDENTIFIER().GetText());
			base.VisitClassDecl(context);
			_semanticAnalyzer.ExitFromBlock();

			_semanticAnalyzer.CurrentPosition = lastPos;

			return null;
		}

		public override Expression? VisitFunctionDecl([NotNull] CEvolParser.FunctionDeclContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var prms = context.@params();

			List<(TypeSpec Type, string Name)> parameters = null!;

			if (prms != null)
			{
				parameters = ParseParams(prms);
			}

			if (parameters == null) parameters = [];

			string? funcName = context.IDENTIFIER().ToString();
			if (funcName == null) throw new NotImplementedException();

			_semanticAnalyzer.EnterToFunction(funcName, parameters);

			Visit(context.block());

			_semanticAnalyzer.ExitFromBlock();
			_semanticAnalyzer.CurrentPosition = lastPos;

			return null;
		}

		public override Expression VisitConstructorDecl([NotNull] CEvolParser.ConstructorDeclContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var prms = context.@params();

			List<(TypeSpec Type, string Name)> parameters = null!;

			if (prms != null)
			{
				parameters = ParseParams(prms);
			}

			if (parameters == null) parameters = [];

			_semanticAnalyzer.EnterToConstructor(parameters);

			Visit(context.block());

			_semanticAnalyzer.ExitFromBlock();
			_semanticAnalyzer.CurrentPosition = lastPos;

			return null;
		}

		public override Expression? VisitBlock([NotNull] CEvolParser.BlockContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			foreach (var statement in context.statement())
			{
				Expression? expr = Visit(statement);
				if (expr != null)
				{
					_semanticAnalyzer.InserToCurrentBlock(expr);
				}
			}

			_semanticAnalyzer.CurrentPosition = lastPos;

			return null;
		}

		public override Expression? VisitVarDeclStmt([NotNull] CEvolParser.VarDeclStmtContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var ctx = context.fieldDecl();

			var typeSpec_ = ParseTypeSpec(ctx.typeSpec());
			if (typeSpec_ == null)
			{
				var errorPos = _semanticAnalyzer.CurrentPosition;
				_semanticAnalyzer.CurrentPosition = lastPos;
				return new StubForErrorExpression(errorPos);
			}

			var typeSpec = typeSpec_.Value;

			var varName = ctx.IDENTIFIER().ToString();
			if (varName == null) throw new NotImplementedException();

			var args = ctx.args();
			Expression varAccessing = _semanticAnalyzer.CreateLocalVariable(varName, typeSpec, args != null ? ParseArgs(args) : null);

			if (ctx.ASSIGN() != null)
			{
				var value = Visit(ctx.expression());
				if (value == null) throw new NotImplementedException();

				Qualifier? qaliffer = typeSpec.QualifiersExists ? typeSpec.Qualifiers[0] : null;
				return _semanticAnalyzer.VariableAssing(varAccessing, value, qaliffer);
			}

			_semanticAnalyzer.CurrentPosition = lastPos;

			return varAccessing;
		}

		public override Expression? VisitExprStmt([NotNull] CEvolParser.ExprStmtContext context)
		{
			return Visit(context.expression());
		}

		public override Expression VisitAssignStmt([NotNull] CEvolParser.AssignStmtContext ctx)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(ctx);

			var context = ctx.assignment();
			var expressions = context.expression();
			if (expressions.Length != 2)
				throw new NotImplementedException();

			var leftExpression = Visit(expressions[0]);
			var rightExpression = Visit(expressions[1]);

			var qualiffer = context.qualifier()?.GetText();

			if (leftExpression == null || rightExpression == null) throw new NotImplementedException();

			// TODO: где-то сделать проверку что это выражение - доступ к переменной, а не каккая-то хуета
			var res = _semanticAnalyzer.VariableAssing(leftExpression, rightExpression, qualiffer != null ? Qualifier.FromString(qualiffer) : null);
			_semanticAnalyzer.CurrentPosition = lastPos;

			return res;
		}

		public override Expression? VisitCallExpr([NotNull] CEvolParser.CallExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			string? funcName = context.IDENTIFIER().ToString();
			if (funcName == null) throw new NotImplementedException();
			var args = context.args();

			var arguments = args != null ? ParseArgs(context.args()) : Array.Empty<Expression>();

			var res = _semanticAnalyzer.CallFunction(funcName, arguments);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		private Expression[] ParseArgs(CEvolParser.ArgsContext context)
		{
			var ars = context?.expression();
			if (ars == null) return Array.Empty<Expression>();
			//return ars.Select(x => (IValueAccessor)Visit(x)).ToArray();
			Expression[] result = new Expression[ars.Length];

			for (int i = 0; i < ars.Length; i++)
			{
				var expr = Visit(ars[i]);
				if (expr == null) throw new NotImplementedException();

				result[i] = expr;
			}

			return result;
		}

		private List<(TypeSpec Type, string Name)> ParseParams([NotNull] CEvolParser.ParamsContext context)
		{
			var parameters = new List<(TypeSpec Type, string Name)>();

			int count = context.typeSpec().Length;

			for (int i = 0; i < count; i++)
			{
				TypeSpec? paramDecl = ParseTypeSpec(context.typeSpec(i));
				string paramName = context.IDENTIFIER(i).GetText();

				if (paramDecl == null) continue;
				parameters.Add((paramDecl.Value, paramName));
			}

			return parameters;
		}

		public override Expression? VisitLocExpr([NotNull] CEvolParser.LocExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var value = Visit(context.expression());
			if (value == null) throw new NotImplementedException();


			var res = _semanticAnalyzer.GetPointerToVar(value);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression VisitRefExpr([NotNull] CEvolParser.RefExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var value = Visit(context.expression());
			if (value == null) throw new NotImplementedException();

			var res = _semanticAnalyzer.SetRefQualifier(value);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitNewExpr([NotNull] CEvolParser.NewExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			string? className = context.IDENTIFIER()?.GetText();
			if (className == null) throw new NotImplementedException();

			var args = context.args();
			var arguments = args != null ? ParseArgs(context.args()) : Array.Empty<Expression>();

			var res = _semanticAnalyzer.CallHeapConstructor(className, arguments);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitNewArrayExpr([NotNull] CEvolParser.NewArrayExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			if (context.arraySizeSpec().Length > 1)
				throw new NotImplementedException(); // TODO: реализовать многомерные массивы

			var arrySizeGettingExpr = ParseArraySizeSpec(context.arraySizeSpec()[0]);

			if (context.IDENTIFIER()?.GetText() == null || arrySizeGettingExpr == null)
				throw new NotImplementedException();

			var res = _semanticAnalyzer.CreateArrayInHeap(context.IDENTIFIER().GetText(), arrySizeGettingExpr);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitIndexExpr([NotNull] CEvolParser.IndexExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			if (context.expression() == null || context.args() == null)
				throw new NotImplementedException();

			Expression? expr = Visit(context.expression());
			if (expr == null)
				throw new NotImplementedException();

			Expression[] args = ParseArgs(context.args());

			if (args.Length != 1)  // TODO: реализовать многомерные массивы
				throw new NotImplementedException();

			var res = _semanticAnalyzer.ArrayCellAccess(expr, args[0]);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public Expression? ParseArraySizeSpec([NotNull] CEvolParser.ArraySizeSpecContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			if (context.expression().Length > 1)
				throw new NotImplementedException(); // TODO: реализовать многомерные массивы

			var res = Visit(context.expression()[0]);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitMemberAccess([NotNull] CEvolParser.MemberAccessContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var expr = Visit(context.expression());
			if (expr == null) throw new NotImplementedException();

			string? memberName = context.IDENTIFIER().GetText();
			if (memberName == null) throw new NotImplementedException();

			if (context.LPAREN() == null)
			{
				var res = _semanticAnalyzer.ClassFieldAccess(expr, memberName);

				_semanticAnalyzer.CurrentPosition = lastPos;
				return res;
			}
			else
			{
				var arguments = ParseArgs(context.args());
				SetCurrentPosition(context.IDENTIFIER().Symbol);
				var res = _semanticAnalyzer.CallClassMethod(memberName, expr, arguments);

				_semanticAnalyzer.CurrentPosition = lastPos;
				return res;
			}
		}

		private TypeSpec? ParseTypeSpec([NotNull] CEvolParser.TypeSpecContext context)
		{
			// TODO: поля классов парсить здесь повторно смысла нет. Надо как-то это все кэшировать
			var typeName = context.IDENTIFIER().GetText();
			if (string.IsNullOrEmpty(typeName))
				throw new NotImplementedException();

			var qualifiers = new List<string>();
			foreach (var qualifier in context.qualifier())
			{
				qualifiers.Add(qualifier.GetText());
			}

			foreach (var arr in context.arraySpec())
			{
				qualifiers.Add(ParseArraySpec(arr));
			}

			var typeDesc = _membersFinder.TryFindType(typeName);
			if (typeDesc == null)
			{
				_semanticAnalyzer.ReportError($"The type '{typeName}' was not found");
				return null;
			}

			return new TypeSpec(typeDesc, Qualifier.FromString(qualifiers));
		}

		public string ParseArraySpec([NotNull] CEvolParser.ArraySpecContext context)
		{
			return "array";
		}

		public override Expression? VisitNumberExpr([NotNull] CEvolParser.NumberExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var value = context.NUMBER().GetText();
			var num = BigInteger.Parse(value);

			if (context.MINUS() != null) num *= -1;

			if (num >= 0 && num <= 255)
			{
				var res = _semanticAnalyzer.CreateByte((byte)num);

				_semanticAnalyzer.CurrentPosition = lastPos;
				return res;
			}
			else
			{
				var res = _semanticAnalyzer.CreateInt((int)num);

				_semanticAnalyzer.CurrentPosition = lastPos;
				return res;
			}
		}

		public override Expression? VisitStringExpr([NotNull] CEvolParser.StringExprContext context)
		{
			var str = context.STRING().GetText();
			return _semanticAnalyzer.CreateString(str);
		}

		public override Expression? VisitReturnStmt([NotNull] CEvolParser.ReturnStmtContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var result = Visit(context.expression());
			if (result == null) throw new NotImplementedException();

			_semanticAnalyzer.BuildReturn(result);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return null;
		}

		public override Expression? VisitIfStmt([NotNull] CEvolParser.IfStmtContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var ctx = context.ifStatement();

			var condition = Visit(ctx.expression());
			if (condition == null) throw new NotImplementedException();

			_semanticAnalyzer.EnterToIfBlock(condition);
			foreach (var statement in ctx.statement())
			{
				Expression? expr = Visit(statement);
				if (expr != null)
				{
					_semanticAnalyzer.InserToCurrentBlock(expr);
				}
			}

			_semanticAnalyzer.ExitFromBlock();

			_semanticAnalyzer.CurrentPosition = lastPos;
			return null;
		}

		public override Expression? VisitWhileStmt([NotNull] CEvolParser.WhileStmtContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var ctx = context.whileStatement();

			var condition = Visit(ctx.expression());
			if (condition == null) throw new NotImplementedException();

			_semanticAnalyzer.EnterToWhileBlock(condition);
			Visit(ctx.block());
			_semanticAnalyzer.ExitFromBlock();

			_semanticAnalyzer.CurrentPosition = lastPos;
			return null;
		}

		public override Expression? VisitIdExpr([NotNull] CEvolParser.IdExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var varName = context.IDENTIFIER().ToString();
			if (varName == null) throw new NotImplementedException();

			var res = _semanticAnalyzer.VariableAccess(varName);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitAddSubExpr([NotNull] CEvolParser.AddSubExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var expressions = context.expression();
			if (expressions.Length != 2) throw new NotImplementedException();

			var leftValue = Visit(expressions[0]);
			var rightValue = Visit(expressions[1]);

			if (leftValue == null || rightValue == null) throw new NotImplementedException();

			Expression res;
			if (context.MINUS() != null) // это минус
			{
				res = _semanticAnalyzer.Sub(leftValue, rightValue);
			}
			else // это плюс
			{
				res = _semanticAnalyzer.Sum(leftValue, rightValue);
			}

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitParenExpr([NotNull] CEvolParser.ParenExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var res = Visit(context.expression());

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitCastExpr([NotNull] CEvolParser.CastExprContext context)
		{
			var lastPos = _semanticAnalyzer.CurrentPosition;
			SetCurrentPosition(context);

			var expression = Visit(context.expression());
			if (expression == null) throw new NotImplementedException();

			var typeSpec = ParseTypeSpec(context.typeSpec());
			if (typeSpec == null)
			{
				var errorPos = _semanticAnalyzer.CurrentPosition;
				_semanticAnalyzer.CurrentPosition = lastPos;
				return new StubForErrorExpression(errorPos);
			}

			var res = _semanticAnalyzer.TypeCast(expression, typeSpec.Value);

			_semanticAnalyzer.CurrentPosition = lastPos;
			return res;
		}

		public override Expression? VisitEqNeqExpr([NotNull] CEvolParser.EqNeqExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());

			CompareOperator compareOperator;
			if (context.NEQ() != null)
				compareOperator = CompareOperator.NotEqual;
			else if (context.EQ() != null)
				compareOperator = CompareOperator.Equal;
			else
				throw new NotImplementedException();

			return _semanticAnalyzer.Compare(left, right, compareOperator);
		}

		public override Expression? VisitLtGtExpr([NotNull] CEvolParser.LtGtExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());

			CompareOperator compareOperator;
			if (context.LT() != null)
				compareOperator = CompareOperator.LessThan;
			else if (context.GT() != null)
				compareOperator = CompareOperator.GreaterThan;
			else
				throw new NotImplementedException();

			return _semanticAnalyzer.Compare(left, right, compareOperator);
		}

		public override Expression? VisitBitAndExpr([NotNull] CEvolParser.BitAndExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());
			return _semanticAnalyzer.BitAnd(left, right);
		}

		public override Expression? VisitBitXorExpr([NotNull] CEvolParser.BitXorExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());
			return _semanticAnalyzer.BitXor(left, right);
		}

		public override Expression? VisitBitOrExpr([NotNull] CEvolParser.BitOrExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());
			return _semanticAnalyzer.BitOr(left, right);
		}

		public override Expression? VisitLogicalAndExpr([NotNull] CEvolParser.LogicalAndExprContext context)
		{
			(Expression left, Expression right) = ParseBinaryExpression(context.expression());
			return _semanticAnalyzer.LogicalAnd(left, right);
		}

		private (Expression left, Expression right) ParseBinaryExpression(CEvolParser.ExpressionContext[]? expressions)
		{
			if (expressions == null || expressions.Length != 2) throw new NotImplementedException();

			var leftValue = Visit(expressions[0]);
			var rightValue = Visit(expressions[1]);

			if (leftValue == null || rightValue == null) throw new NotImplementedException();

			return (leftValue, rightValue);
		}

		private void SetCurrentPosition(ParserRuleContext context)
		{
			SetCurrentPosition(context.Start);
		}

		private void SetCurrentPosition(IToken context)
		{
			_semanticAnalyzer.CurrentPosition = new PositionInSources(_currentFile, context.Line, context.Column);
		}

	}
}
