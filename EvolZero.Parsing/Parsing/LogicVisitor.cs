using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using EvolZero.Core;
using EvolZero.Core.Analysis;
using EvolZero.Core.Analysis.Semantic;
using EvolZero.Core.LogicModels.Expressions;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Parsing.Models;
using System.Numerics;
using static CEvolParser;


namespace EvolZero.Parsing
{
    public class LogicVisitor : CEvolParserBaseVisitor<Expression?>
    {
        private readonly MembersFinder _membersFinder;
        private readonly ISemanticTokensConsumer _tokensCunsumer;
        private readonly string _currentFile;
        private readonly SemanticTreeBuilder _semanticAnalyzer;
        private readonly TypeAnalyzer _typeAnalyzer;

        public Statement ResultStatement { get; private set; }

        private bool _currentClassDestructorHandled = false; // TODO: эту логику от сюда куда-нибудь вынести, здесь ее быть не должно
        private bool _currentClassConstructorHandled = false;
        private List<(string Name, FieldDeclContext Init)> _currentClassFieldInitializers = new();

        public LogicVisitor(MembersFinder membersFinder, ISemanticTokensConsumer tokensCunsumer, ErrorsBag errorsBag, string currentFile)
        {
            _typeAnalyzer = new TypeAnalyzer(membersFinder);
            _semanticAnalyzer = new SemanticTreeBuilder(membersFinder, _typeAnalyzer, errorsBag);
            _membersFinder = membersFinder;
            _tokensCunsumer = tokensCunsumer;
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

            if (_tokensCunsumer != null)
                PushToken([context.NAMESPACE(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Namespace]);

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

            if (_tokensCunsumer != null)
                PushToken([context.USING(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Namespace]);

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

            if (_tokensCunsumer != null)
                PushToken([context.CLASS(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Class]);

            _semanticAnalyzer.EnterToClass(context.IDENTIFIER().GetText());

            _currentClassConstructorHandled = false;
            _currentClassFieldInitializers = context.fieldDecl()
                .Where(f => f.ASSIGN() != null)
                .Select(f => (f.IDENTIFIER().GetText(), f))
                .ToList();

            base.VisitClassDecl(context);

            if (!_currentClassConstructorHandled && (_currentClassFieldInitializers.Count > 0 || _semanticAnalyzer.CurrentClassHasRefFields()))
            {
                _semanticAnalyzer.EnterToConstructor([]);
                InjectFieldInitializers();
                _semanticAnalyzer.ExitFromBlock();
            }

            if (!_currentClassDestructorHandled)
            {
                _semanticAnalyzer.EnterToDestructor();
                _semanticAnalyzer.ExitFromBlock();
            }

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;
            _currentClassDestructorHandled = false;
            _currentClassConstructorHandled = false;
            _currentClassFieldInitializers = new();

            return null;
        }

        private void InjectFieldInitializers()
        {
            foreach (var (name, initExpr) in _currentClassFieldInitializers)
            {
                SetCurrentPosition(initExpr);
                var value = Visit(initExpr);
                if (value == null) continue;

                var assign = _semanticAnalyzer.FieldAssing(name, value);
                _semanticAnalyzer.InserToCurrentBlock(assign);
            }
        }

        public override Expression? VisitFieldDecl([NotNull] CEvolParser.FieldDeclContext context)
        {
            if (_tokensCunsumer != null)
            {
                PushAccessModifierToken(context.accessModifier());
                PushTypeSpecTokens(context.typeSpec());
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Property]);
            }

            var expr = context.expression();
            if (expr == null) return null;

            return Visit(expr);
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

            if (_tokensCunsumer != null && funcName != null)
            {
                PushTypeSpecTokens(context.typeSpec());
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Function]);
            }

            if (funcName == null) throw new NotImplementedException();

            _semanticAnalyzer.EnterToFunction(funcName, parameters);

            Visit(context.block());

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;

            return null;
        }

        public override Expression? VisitConstructorDecl([NotNull] CEvolParser.ConstructorDeclContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            if (_tokensCunsumer != null)
            {
                PushAccessModifierToken(context.accessModifier());
                PushToken([context.CONSTRUCTOR()], [SemanticTokenType.Keyword]);
            }

            var prms = context.@params();

            List<(TypeSpec Type, string Name)> parameters = null!;

            if (prms != null)
            {
                parameters = ParseParams(prms);
            }

            if (parameters == null) parameters = [];

            _currentClassConstructorHandled = true;

            _semanticAnalyzer.EnterToConstructor(parameters);

            InjectFieldInitializers();

            Visit(context.block());

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;

            return null;
        }

        public override Expression? VisitDesctructorDecl([NotNull] DesctructorDeclContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            if (_tokensCunsumer != null)
                PushToken([context.DESTRUCTOR()], [SemanticTokenType.Keyword]);

            _semanticAnalyzer.EnterToDestructor();

            Visit(context.block());

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;
            _currentClassDestructorHandled = true;

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

            if (_tokensCunsumer != null && varName != null)
            {
                PushTypeSpecTokens(ctx.typeSpec());
                PushToken([ctx.IDENTIFIER()], [SemanticTokenType.Variable]);
            }

            if (varName == null) throw new NotImplementedException();

            Expression varAccessing = _semanticAnalyzer.CreateLocalVariable(varName, typeSpec);

            if (ctx.ASSIGN() != null)
            {
                var value = Visit(ctx.expression());
                if (value == null) throw new NotImplementedException();

                return _semanticAnalyzer.VariableAssing(varAccessing, value);
            }

            _semanticAnalyzer.CurrentPosition = lastPos;

            return varAccessing;
        }

        public override Expression? VisitExprStmt([NotNull] CEvolParser.ExprStmtContext context)
        {
            return Visit(context.expression());
        }

        public override Expression VisitAssignExpr([NotNull] CEvolParser.AssignExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var expressions = context.expression();
            if (expressions.Length != 2)
                throw new NotImplementedException();

            if (_tokensCunsumer != null)
                PushToken([context.ASSIGN()], [SemanticTokenType.Operator]);

            var leftExpression = Visit(expressions[0]);
            var rightExpression = Visit(expressions[1]);

            if (leftExpression == null || rightExpression == null) throw new NotImplementedException();

            // TODO: где-то сделать проверку что это выражение - доступ к переменной, а не каккая-то хуета

            var res = _semanticAnalyzer.VariableAssing(leftExpression, rightExpression);
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

            if (_tokensCunsumer != null)
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Function]);

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

                if (_tokensCunsumer != null)
                    PushToken([context.IDENTIFIER(i)], [SemanticTokenType.Variable]);

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

            if (_tokensCunsumer != null)
                PushToken([context.LOC()], [SemanticTokenType.Keyword]);

            var res = _semanticAnalyzer.GetPointerToVar(value);

            _semanticAnalyzer.CurrentPosition = lastPos;
            return res;
        }

        public override Expression VisitQualifierExpr([NotNull] CEvolParser.QualifierExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            if (_tokensCunsumer != null)
                PushQualifierToken(context.qualifier());

            var value = Visit(context.expression());
            if (value == null) throw new NotImplementedException();

            Qualifier qualifier = new QualifierWorkpiece(context.qualifier().GetText()).ToQualifier();
            var res = _semanticAnalyzer.SetQualifier(value, qualifier);

            _semanticAnalyzer.CurrentPosition = lastPos;
            return res;
        }

        public override Expression? VisitNewExpr([NotNull] NewExprContext context)
        {
            if (_tokensCunsumer != null)
                PushToken([context.NEW()], [SemanticTokenType.Keyword]);

            return VisitObjectInstantiation(context, context.typeSpec(),
                context.args(), context.LPAREN(), context.objectInitializer(), true);
        }

        public override Expression? VisitValExpr([NotNull] ValExprContext context)
        {
            if (_tokensCunsumer != null)
                PushToken([context.VAL()], [SemanticTokenType.Keyword]);

            return VisitObjectInstantiation(context, context.typeSpec(), context.args(),
                context.LPAREN(), context.objectInitializer(), false);
        }

        private Expression? VisitObjectInstantiation(ParserRuleContext context, TypeSpecContext typeSpecContext, ArgsContext? argsContext,
            ITerminalNode? lparenNode, ObjectInitializerContext? objectInitializerContext, bool inHeap)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var typeSpec = ParseTypeSpec(typeSpecContext);
            if (typeSpec == null)
            {
                var errorPos = _semanticAnalyzer.CurrentPosition;
                _semanticAnalyzer.CurrentPosition = lastPos;
                return new StubForErrorExpression(errorPos);
            }

            Expression[]? arguments = null;
            if (argsContext != null)
            {
                arguments = ParseArgs(argsContext);
            }
            else if (lparenNode != null)
            {
                arguments = Array.Empty<Expression>();
            }

            Expression? objectInitializer = null;
            if (objectInitializerContext != null)
            {
                objectInitializer = Visit(objectInitializerContext);
            }

            var res = _semanticAnalyzer.CreateObject(typeSpec.Value, arguments, objectInitializer, inHeap);

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
                if (_tokensCunsumer != null)
                    PushToken([context.IDENTIFIER()], [SemanticTokenType.Property]);

                var res = _semanticAnalyzer.ClassFieldAccess(expr, memberName);

                _semanticAnalyzer.CurrentPosition = lastPos;
                return res;
            }
            else
            {
                if (_tokensCunsumer != null)
                    PushToken([context.IDENTIFIER()], [SemanticTokenType.Method]);

                var arguments = ParseArgs(context.args());
                SetCurrentPosition(context.IDENTIFIER().Symbol);
                var res = _semanticAnalyzer.CallClassMethod(memberName, expr, arguments);

                _semanticAnalyzer.CurrentPosition = lastPos;
                return res;
            }
        }

        private void PushTypeSpecTokens(TypeSpecContext? context)
        {
            if (context == null) return;
            PushToken([context.IDENTIFIER()], [SemanticTokenType.Type]);

            var qualifers = context.qualifier();
            if (qualifers == null) return;

            foreach (var qualifier in qualifers)
            {
                PushQualifierToken(qualifier);
            }
        }

        private void PushQualifierToken(QualifierContext? context)
        {
            if (context == null) return;
            if (context.REF() != null)
            {
                PushToken([context.REF()], [SemanticTokenType.Keyword]);
            }
            else if (context.REFB() != null)
            {
                PushToken([context.REFB()], [SemanticTokenType.Keyword]);
            }
        }

        private void PushAccessModifierToken(AccessModifierContext? context)
        {
            if (context != null)
            {
                if (context.PUBLIC() != null)
                {
                    PushToken([context.PUBLIC()], [SemanticTokenType.Keyword]);
                }
                else if (context.PRIVATE() != null)
                {
                    PushToken([context.PRIVATE()], [SemanticTokenType.Keyword]);
                }
            }
        }

        private TypeSpec? ParseTypeSpec([NotNull] CEvolParser.TypeSpecContext context)
        {
            // TODO: поля классов парсить здесь повторно смысла нет. Надо как-то это все кэшировать
            var typeName = context.IDENTIFIER().GetText();
            if (string.IsNullOrEmpty(typeName))
                throw new NotImplementedException();

            if (_tokensCunsumer != null)
                PushTypeSpecTokens(context);

            var qualifiers = ParsingCommon.ParseQualifiers(context);

            var typeDesc = _membersFinder.TryFindType(typeName);
            if (typeDesc == null)
            {
                _semanticAnalyzer.ReportError($"The type '{typeName}' was not found"); // TODO: че-то хуета какая-то, тут ошибки не должны генериться
                return null;
            }

            return new TypeSpec(typeDesc, QualifierWorkpiece.ToQualifiers(qualifiers));
        }

        public override Expression? VisitNumberExpr([NotNull] CEvolParser.NumberExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var value = context.NUMBER().GetText();
            var num = BigInteger.Parse(value);

            if (_tokensCunsumer != null)
                PushToken([context.NUMBER()], [SemanticTokenType.Number]);

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
            if (_tokensCunsumer != null)
                PushToken([context.STRING()], [SemanticTokenType.String]);

            var str = context.STRING().GetText();
            return _semanticAnalyzer.CreateString(str);
        }

        public override Expression? VisitReturnStmt([NotNull] CEvolParser.ReturnStmtContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            if (_tokensCunsumer != null)
                PushToken([context.RETURN()], [SemanticTokenType.Operator]);

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

            if (_tokensCunsumer != null)
                PushToken([ctx.IF()], [SemanticTokenType.Operator]);

            var condition = Visit(ctx.expression());
            if (condition == null) throw new NotImplementedException();

            _semanticAnalyzer.EnterToIfBlock(condition);
            Visit(ctx.block());

            var ifBlock = _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;

            foreach (var statement in ctx.elseIfStatement())
            {
                HendleElseIf(statement, ifBlock);
            }

            var elseBlock = ctx.elseStatement();
            if (elseBlock != null)
            {
                HendleElse(elseBlock, ifBlock);
            }

            return null;
        }

        private void HendleElseIf(ElseIfStatementContext ctx, Statement ifBlock)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(ctx);

            if (_tokensCunsumer != null)
                PushToken([ctx.ELSEIF()], [SemanticTokenType.Operator]);

            var elseIfCondition = Visit(ctx.expression());
            if (elseIfCondition == null) throw new NotImplementedException();

            _semanticAnalyzer.EnterToElseIfBlock(ifBlock, elseIfCondition);

            Visit(ctx.block());

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;
        }

        private void HendleElse(ElseStatementContext ctx, Statement ifBlock)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(ctx);

            if (_tokensCunsumer != null)
                PushToken([ctx.ELSE()], [SemanticTokenType.Operator]);

            _semanticAnalyzer.EnterToElseBlock(ifBlock);
            Visit(ctx.block());

            _semanticAnalyzer.ExitFromBlock();
            _semanticAnalyzer.CurrentPosition = lastPos;
        }

        public override Expression? VisitWhileStmt([NotNull] CEvolParser.WhileStmtContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var ctx = context.whileStatement();

            if (_tokensCunsumer != null)
                PushToken([ctx.WHILE()], [SemanticTokenType.Operator]);

            var condition = Visit(ctx.expression());
            if (condition == null) throw new NotImplementedException();

            _semanticAnalyzer.EnterToWhileBlock(condition);
            Visit(ctx.block());
            _semanticAnalyzer.ExitFromBlock();

            _semanticAnalyzer.CurrentPosition = lastPos;
            return null;
        }

        public override Expression? VisitUnsafeStmt([NotNull] CEvolParser.UnsafeStmtContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            if (_tokensCunsumer != null)
                PushToken([context.unsafeStatement().UNSAFE()], [SemanticTokenType.Keyword]);

            _semanticAnalyzer.EnterUnsafeBlock();
            Visit(context.unsafeStatement().block());
            _semanticAnalyzer.ExitFromBlock();

            _semanticAnalyzer.CurrentPosition = lastPos;
            return null;
        }

        public override Expression VisitObjectInitializer([NotNull] ObjectInitializerContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var res = new List<Expression>();
            if (context.objectInitializerParams() != null)
            {
                foreach (var param in context.objectInitializerParams().expression())
                {
                    var expr = Visit(param);
                    if (expr != null) res.Add(expr);
                }
            }

            _semanticAnalyzer.CurrentPosition = lastPos;
            return _semanticAnalyzer.ObjectInitializer(res);
        }

        public override Expression? VisitIdExpr([NotNull] CEvolParser.IdExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            var varName = context.IDENTIFIER().ToString();
            if (varName == null) throw new NotImplementedException();

            if (_tokensCunsumer != null)
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Variable]);

            var res = _semanticAnalyzer.VariableAccess(varName);

            _semanticAnalyzer.CurrentPosition = lastPos;
            return res;
        }

        public override Expression? VisitAddSubExpr([NotNull] CEvolParser.AddSubExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            (Expression leftValue, Expression rightValue) = ParseBinaryExpression(context.expression());

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

        public override Expression VisitMulDivExpr([NotNull] MulDivExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            (Expression leftValue, Expression rightValue) = ParseBinaryExpression(context.expression());

            Expression res;
            if (context.MUL() != null) // это умножить
            {
                res = _semanticAnalyzer.Multiple(leftValue, rightValue);
            }
            else // это разделить
            {
                res = _semanticAnalyzer.Division(leftValue, rightValue);
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

        public override Expression? VisitExchangeExpr([NotNull] CEvolParser.ExchangeExprContext context)
        {
            var lastPos = _semanticAnalyzer.CurrentPosition;
            SetCurrentPosition(context);

            (Expression leftValue, Expression rightValue) = ParseBinaryExpression(context.expression());

            var res = _semanticAnalyzer.Exchange(leftValue, rightValue);

            _semanticAnalyzer.CurrentPosition = lastPos;
            return res;
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
            _semanticAnalyzer.CurrentPosition = new PositionInSources(_currentFile, context.Line, context.Column, context.StopIndex);
        }

        private void PushToken(ITerminalNode[] nodes, SemanticTokenType[] types)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var sym = nodes[i].Symbol;
                var position = new PositionInSources(_currentFile, sym.Line, sym.Column, sym.Column + sym.Text.Length - 1);
                _tokensCunsumer!.Push(position, types[i]);
            }
        }

    }
}
