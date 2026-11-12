using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using EvolZero.Core;
using static CEvolParser;

namespace EvolZero.Parsing
{
    public class SemanticTokensVisitor : CEvolParserBaseVisitor<object?>
    {
        private readonly ISemanticTokensConsumer _tokensConsumer;
        private readonly string _currentFile;

        public SemanticTokensVisitor(ISemanticTokensConsumer tokensConsumer, string currentFile)
        {
            _tokensConsumer = tokensConsumer ?? throw new ArgumentNullException(nameof(tokensConsumer));
            _currentFile = currentFile;
        }

        public override object? VisitNamespaceDecl([NotNull] NamespaceDeclContext context)
        {
            PushToken([context.NAMESPACE(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Namespace]);
            return base.VisitNamespaceDecl(context);
        }

        public override object? VisitUsingDecl([NotNull] UsingDeclContext context)
        {
            PushToken([context.USING(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Namespace]);
            return base.VisitUsingDecl(context);
        }

        public override object? VisitClassDecl([NotNull] ClassDeclContext context)
        {
            PushToken([context.CLASS(), context.IDENTIFIER()], [SemanticTokenType.Keyword, SemanticTokenType.Class]);
            return base.VisitClassDecl(context);
        }

        public override object? VisitFieldDecl([NotNull] FieldDeclContext context)
        {
            PushAccessModifierToken(context.accessModifier());
            PushTypeSpecTokens(context.typeSpec());
            PushToken([context.IDENTIFIER()], [SemanticTokenType.Property]);

            return base.VisitFieldDecl(context);
        }

        public override object? VisitFunctionDecl([NotNull] FunctionDeclContext context)
        {
            PushExtraModifiersTokens(context.extraModifier());
            PushAccessModifierToken(context.accessModifier());
            PushTypeSpecTokens(context.typeSpec());
            PushToken([context.IDENTIFIER()], [SemanticTokenType.Function]);
            PushLifetimesTokens(context.lifetimesDecl());

            if (context.@params() != null)
                ParseParams(context.@params());

            return base.VisitFunctionDecl(context);
        }

        public override object? VisitAbstractFunctionDecl([NotNull] AbstractFunctionDeclContext context)
        {
            PushExtraModifiersTokens(context.extraModifier());
            PushAccessModifierToken(context.accessModifier());
            PushTypeSpecTokens(context.typeSpec());
            PushToken([context.IDENTIFIER()], [SemanticTokenType.Function]);

            if (context.@params() != null)
                ParseParams(context.@params());

            return base.VisitAbstractFunctionDecl(context);
        }

        public override object? VisitConstructorDecl([NotNull] ConstructorDeclContext context)
        {
            PushExtraModifiersTokens(context.extraModifier());
            PushAccessModifierToken(context.accessModifier());
            PushToken([context.CONSTRUCTOR()], [SemanticTokenType.Keyword]);
            PushLifetimesTokens(context.lifetimesDecl());

            if (context.@params() != null)
                ParseParams(context.@params());

            return base.VisitConstructorDecl(context);
        }

        public override object? VisitDesctructorDecl([NotNull] DesctructorDeclContext context)
        {
            PushExtraModifiersTokens(context.extraModifier());
            PushAccessModifierToken(context.accessModifier());
            PushToken([context.DESTRUCTOR()], [SemanticTokenType.Keyword]);

            return base.VisitDesctructorDecl(context);
        }

        public override object? VisitVarDeclStmt([NotNull] VarDeclStmtContext context)
        {
            var ctx = context.fieldDecl();
            if (ctx != null)
            {
                PushTypeSpecTokens(ctx.typeSpec());
                if (ctx.IDENTIFIER() != null)
                    PushToken([ctx.IDENTIFIER()], [SemanticTokenType.Variable]);

                if (ctx.expression() != null)
                    Visit(ctx.expression());
            }

            // Не вызываем base.VisitVarDeclStmt, чтобы fieldDecl внутри локальной переменной
            // не обработался методом VisitFieldDecl как свойство/поле класса
            return null;
        }

        public override object? VisitAssignExpr([NotNull] AssignExprContext context)
        {
            if (context.ASSIGN() != null)
                PushToken([context.ASSIGN()], [SemanticTokenType.Operator]);

            return base.VisitAssignExpr(context);
        }

        public override object? VisitCallExpr([NotNull] CallExprContext context)
        {
            if (context.IDENTIFIER() != null)
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Function]);

            return base.VisitCallExpr(context);
        }

        public override object? VisitLocExpr([NotNull] LocExprContext context)
        {
            if (context.LOC() != null)
                PushToken([context.LOC()], [SemanticTokenType.Keyword]);

            return base.VisitLocExpr(context);
        }

        public override object? VisitQualifierExpr([NotNull] QualifierExprContext context)
        {
            PushQualifierToken(context.qualifier());
            return base.VisitQualifierExpr(context);
        }

        public override object? VisitNewExpr([NotNull] NewExprContext context)
        {
            if (context.NEW() != null)
                PushToken([context.NEW()], [SemanticTokenType.Keyword]);

            PushTypeSpecTokens(context.typeSpec());
            return base.VisitNewExpr(context);
        }

        public override object? VisitValExpr([NotNull] ValExprContext context)
        {
            if (context.VAL() != null)
                PushToken([context.VAL()], [SemanticTokenType.Keyword]);

            PushTypeSpecTokens(context.typeSpec());
            return base.VisitValExpr(context);
        }

        public override object? VisitCastExpr([NotNull] CastExprContext context)
        {
            PushTypeSpecTokens(context.typeSpec());
            return base.VisitCastExpr(context);
        }

        public override object? VisitMemberAccess([NotNull] MemberAccessContext context)
        {
            if (context.IDENTIFIER() != null)
            {
                var tokenType = context.LPAREN() == null ? SemanticTokenType.Property : SemanticTokenType.Method;
                PushToken([context.IDENTIFIER()], [tokenType]);
            }

            return base.VisitMemberAccess(context);
        }

        public override object? VisitIdExpr([NotNull] IdExprContext context)
        {
            if (context.IDENTIFIER() != null)
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Variable]);

            return base.VisitIdExpr(context);
        }

        public override object? VisitNumberExpr([NotNull] NumberExprContext context)
        {
            if (context.NUMBER() != null)
                PushToken([context.NUMBER()], [SemanticTokenType.Number]);

            return base.VisitNumberExpr(context);
        }

        public override object? VisitStringExpr([NotNull] StringExprContext context)
        {
            if (context.STRING() != null)
                PushToken([context.STRING()], [SemanticTokenType.String]);

            return base.VisitStringExpr(context);
        }

        public override object? VisitReturnStmt([NotNull] ReturnStmtContext context)
        {
            if (context.RETURN() != null)
                PushToken([context.RETURN()], [SemanticTokenType.Operator]);

            return base.VisitReturnStmt(context);
        }

        public override object? VisitIfStmt([NotNull] IfStmtContext context)
        {
            var ctx = context.ifStatement();
            if (ctx.IF() != null)
                PushToken([ctx.IF()], [SemanticTokenType.Operator]);

            foreach (var elseIf in ctx.elseIfStatement())
            {
                if (elseIf.ELSEIF() != null)
                    PushToken([elseIf.ELSEIF()], [SemanticTokenType.Operator]);
            }

            if (ctx.elseStatement()?.ELSE() != null)
            {
                PushToken([ctx.elseStatement().ELSE()], [SemanticTokenType.Operator]);
            }

            return base.VisitIfStmt(context);
        }

        public override object? VisitWhileStmt([NotNull] WhileStmtContext context)
        {
            if (context.whileStatement().WHILE() != null)
                PushToken([context.whileStatement().WHILE()], [SemanticTokenType.Operator]);

            return base.VisitWhileStmt(context);
        }

        public override object? VisitUnsafeStmt([NotNull] UnsafeStmtContext context)
        {
            if (context.unsafeStatement().UNSAFE() != null)
                PushToken([context.unsafeStatement().UNSAFE()], [SemanticTokenType.Keyword]);

            return base.VisitUnsafeStmt(context);
        }


        private void ParseParams([NotNull] ParamsContext context)
        {
            int count = context.typeSpec().Length;
            for (int i = 0; i < count; i++)
            {
                PushTypeSpecTokens(context.typeSpec(i));
                if (context.IDENTIFIER(i) != null)
                    PushToken([context.IDENTIFIER(i)], [SemanticTokenType.Variable]);
            }
        }

        private void PushTypeSpecTokens(TypeSpecContext? context)
        {
            if (context == null) return;
            if (context.IDENTIFIER() != null)
                PushToken([context.IDENTIFIER()], [SemanticTokenType.Type]);

            var qualifiers = context.qualifier();
            if (qualifiers == null) return;

            foreach (var qualifier in qualifiers)
            {
                PushQualifierToken(qualifier);
            }
        }

        private void PushQualifierToken(QualifierContext? context)
        {
            if (context == null) return;
            if (context.REF() != null)
                PushToken([context.REF()], [SemanticTokenType.Keyword]);
            else if (context.REFB() != null)
                PushToken([context.REFB()], [SemanticTokenType.Keyword]);
        }

        private void PushAccessModifierToken(AccessModifierContext? context)
        {
            if (context == null) return;
            if (context.PUBLIC() != null)
                PushToken([context.PUBLIC()], [SemanticTokenType.Keyword]);
            else if (context.PRIVATE() != null)
                PushToken([context.PRIVATE()], [SemanticTokenType.Keyword]);
        }

        private void PushExtraModifiersTokens(ExtraModifierContext[]? contexts)
        {
            if (contexts == null) return;
            foreach (var context in contexts)
            {
                if (context == null) continue;

                if (context.STATIC() != null)
                    PushToken([context.STATIC()], [SemanticTokenType.Keyword]);
                else if (context.READONLY() != null)
                    PushToken([context.READONLY()], [SemanticTokenType.Keyword]);
                else if (context.EXTERN() != null)
                    PushToken([context.EXTERN()], [SemanticTokenType.Keyword]);
                else if (context.INFARGS() != null)
                    PushToken([context.INFARGS()], [SemanticTokenType.Keyword]);
            }
        }

        private void PushLifetimesTokens(LifetimesDeclContext? context)
        {
            if (context == null) return;

            if (context.LIFETIMES() != null)
                PushToken([context.LIFETIMES()], [SemanticTokenType.Keyword]);

            var args = context.lifetimesArgs()?.lifetimeDecl();
            if (args == null) return;

            foreach (var arg in args)
            {
                var members = arg.lifetimeMember();
                if (members == null) continue;

                foreach (var member in members)
                {
                    if (member.RETURN() != null)
                        PushToken([member.RETURN()], [SemanticTokenType.Operator]);
                    else if (member.THIS() != null)
                        PushToken([member.THIS()], [SemanticTokenType.Keyword]);
                    else if (member.IDENTIFIER() != null)
                        PushToken([member.IDENTIFIER()], [SemanticTokenType.Variable]);
                }
            }
        }

        private void PushToken(ITerminalNode?[] nodes, SemanticTokenType[] types)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null) continue;

                var sym = node.Symbol;
                var position = new PositionInSources(_currentFile, sym.Line, sym.Column, sym.Column + sym.Text.Length - 1);
                _tokensConsumer.Push(position, types[i]);
            }
        }

    }
}