using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using EvolZero.Core;
using EvolZero.Core.Analysis;
using EvolZero.Core.Analysis.Semantic;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Parsing;
using EvolZero.Parsing.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Concurrent;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EvolZero.LSP
{
    public class AnalysisService
    {
        // Потокобезопасное хранилище ошибок для каждого открытого файла
        private readonly ConcurrentDictionary<DocumentUri, string> _filesChache = new();
        private readonly SemanticTokensConsumer _tokensConsumer = new();
        private string _projectRoot;

        public async Task InitializeProject(string rootPath)
        {
            _projectRoot = rootPath;

            var allFiles = Directory.GetFiles(_projectRoot, "*.cev", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                var text = File.ReadAllText(filePath);
                var uri = DocumentUri.FromFileSystemPath(filePath);
                string content = await File.ReadAllTextAsync(filePath);

                _filesChache[uri] = content;
            }
        }

        public async Task<List<Diagnostic>> FileChanged(DocumentUri uri, string? fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileContent))
                fileContent = await File.ReadAllTextAsync(uri.ToUri().LocalPath);

            _filesChache[uri] = fileContent;
            _tokensConsumer.Clear(uri.ToString());

            var errors = Execute();
            var diagnostics = errors.Where(x => x.Pos.SourceFile == uri.ToString())
                .Select(x => new Diagnostic()
                {
                    Range = new Range(new Position(x.Pos.Line - 1, x.Pos.Symbol), new Position(x.Pos.Line - 1, x.Pos.EndSymbol)),
                    Severity = DiagnosticSeverity.Error,
                    Message = $"{x.ErrorCode} [{x.Layer}]: {x.Comment}",
                    Source = x.Layer
                }).ToList();

            return diagnostics;
        }

        public IReadOnlyList<(PositionInSources pos, Core.SemanticTokenType tokenType)> GetPositions(DocumentUri file)
        {
            return _tokensConsumer.GetPositions(file.ToString());
        }

        private class FileUnit
        {
            public string FilePath { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IParseTree Tree { get; set; } = null!;
            public CEvolLexer Lexer { get; set; } = null!;
            public MembersTableBuilder TableBuilder { get; set; } = null!;
            public string Namespace { get; set; } = null!;
        }

        private MembersTable BuildBaseMembersTable(ICodeGenerator codeGenerator)
        {
            var types = new Dictionary<string, TypeDesc>();
            types["void"] = new TypeDesc("void", codeGenerator.GetType(BaseTypes.Void));
            types["bool"] = new TypeDesc("bool", codeGenerator.GetType(BaseTypes.Bool));
            types["byte"] = new IntegerTypeDesc("byte", codeGenerator.GetType(BaseTypes.Byte));
            types["short"] = new IntegerTypeDesc("short", codeGenerator.GetType(BaseTypes.Short));
            types["int"] = new IntegerTypeDesc("int", codeGenerator.GetType(BaseTypes.Int));
            types["long"] = new IntegerTypeDesc("long", codeGenerator.GetType(BaseTypes.Long));
            types["sbyte"] = new IntegerTypeDesc("sbyte", codeGenerator.GetType(BaseTypes.Byte));
            types["ushort"] = new IntegerTypeDesc("ushort", codeGenerator.GetType(BaseTypes.Short));
            types["uint"] = new IntegerTypeDesc("uint", codeGenerator.GetType(BaseTypes.Int));
            types["ulong"] = new IntegerTypeDesc("long", codeGenerator.GetType(BaseTypes.Long));
            types["float"] = new FloatTypeDesc("float", codeGenerator.GetType(BaseTypes.Float));
            types["double"] = new FloatTypeDesc("double", codeGenerator.GetType(BaseTypes.Double));

            types["short"].CanExpandedTo.Add(types["int"]);
            types["sbyte"].CanExpandedTo.Add(types["short"]);

            types["ushort"].CanExpandedTo.AddRange(types["uint"], types["int"]);
            types["byte"].CanExpandedTo.AddRange(types["short"], types["ushort"]);

            types["int"].CanExpandedTo.AddRange(types["long"], types["float"]);
            types["uint"].CanExpandedTo.AddRange(types["long"], types["ulong"], types["float"]);

            types["long"].CanExpandedTo.Add(types["double"]);
            types["ulong"].CanExpandedTo.AddRange(types["double"]);
            types["float"].CanExpandedTo.Add(types["double"]);

            types[AnonymousObjectTypeDesc.TYPE_NAME] = new AnonymousObjectTypeDesc();

            return new MembersTable([], types);
        }

        public IReadOnlyList<ErrorsBag.Error> Execute()
        {
            var sourcesMap = new Dictionary<string, string>();
            var units = new List<FileUnit>();

            var errorsBag = new ErrorsBag(sourcesMap);

            foreach (var file in _filesChache)
            {
                var fileContent = file.Value;
                string filePath = file.Key.ToString();
                sourcesMap[filePath] = fileContent;

                ICharStream stream = CharStreams.fromString(fileContent);
                var lexer = new CEvolLexer(stream);

                CommonTokenStream tokenStream = new CommonTokenStream(lexer);
                tokenStream.Fill();

                // TODO: хуй знает, это какой-то костыль для вывода комментариев. Возможно по-другмоу сделать
                var commentPositions = tokenStream.GetTokens()
                    .Where(t => t.Type == CEvolLexer.LINE_COMMENT)
                    .Select(t => new
                    {
                        t.Line,
                        t.Column,
                        End = t.Column + t.Text.Length - 1
                    });

                foreach (var comment in commentPositions)
                {
                    _tokensConsumer.Push(new PositionInSources(filePath, comment.Line,
                        comment.Column, comment.End), Core.SemanticTokenType.Comment);
                }

                var parser = new CEvolParser(tokenStream);

                parser.RemoveErrorListeners();
                parser.AddErrorListener(new SyntaxErrorsConsumer(errorsBag, filePath));

                IParseTree tree = parser.program();

                units.Add(new FileUnit
                {
                    FilePath = filePath,
                    Content = fileContent,
                    Tree = tree,
                    Lexer = lexer
                });
            }

            foreach (var unit in units)
            {
                var visitor = new SemanticTokensVisitor(_tokensConsumer, unit.FilePath);
                visitor.Visit(unit.Tree);
            }

            // если есть синтаксические ошибки то зразу же их возвращаем
            if (errorsBag.HasErrors)
            {
                return errorsBag.GetErrors();
            }

            var codeGenerator = new CodeGeneratorStub();

            var globalMembersTable = BuildBaseMembersTable(codeGenerator);
            var globalTypesList = new Dictionary<string, TypeDesc>(globalMembersTable.Types);

            foreach (var unit in units)
            {
                var visitor = new MembersVisitor(errorsBag, unit.FilePath);
                visitor.Visit(unit.Tree);

                var membersTableBuilder = new MembersTableBuilder(visitor.CurrentNameSpace,
                    visitor.Usings, visitor.Classes, visitor.SingleFunctions, codeGenerator);

                membersTableBuilder.PrepareTable(globalTypesList);
                unit.TableBuilder = membersTableBuilder;
                unit.Namespace = visitor.CurrentNameSpace;
            }

            foreach (var unit in units)
            {
                var currentTable = unit.TableBuilder.Build();
                globalMembersTable.Merge(currentTable);
            }

            var finder = new MembersFinder(globalMembersTable);
            foreach (var unit in units)
            {
                if (!string.IsNullOrEmpty(unit.Namespace))
                {
                    finder.AddNamespace(unit.Namespace);
                }
            }

            var program = new ProgramStatement(default);

            var cycles = globalMembersTable.CheckCyclicalDependence();
            if (cycles.Count > 0)
            {
                foreach (var item in cycles)
                {
                    // TODO: сюда передавать позицию в исходниках и сделать нормалььный текст ошибки, код
                    errorsBag.AddError("BasicSemanticsValidator", "code",
                        $"Класс {item.Type.Name} содержит сам себя в поле {item.FiledName}", new PositionInSources("", 0, 0, 0));
                }
            }

            foreach (var unit in units)
            {
                var logicVisitor = new LogicVisitor(finder, errorsBag, unit.FilePath);
                logicVisitor.Visit(unit.Tree);
                var statement = logicVisitor.ResultStatement;

                program.AddStatement(statement);

            }

            if (errorsBag.HasErrors)
            {
                return errorsBag.GetErrors();
            }

            var referencesAnalazer = new LifeTimesAnalyzer(errorsBag, new LiftimesConsumer(finder));
            referencesAnalazer.Visit(program);

            var accessAnalazer = new AccessAnalyzer(errorsBag);
            accessAnalazer.Visit(program);

            return errorsBag.GetErrors();
        }
    }
}
