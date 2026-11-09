using Antlr4.Runtime;
using EvolZero.Core.Analysis.Semantic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using static Antlr4.Runtime.Atn.SemanticContext;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EvolZero.LSP
{
    public class SemanticTokensHandler : SemanticTokensHandlerBase
    {
        private readonly ILogger _logger;

        public SemanticTokensHandler(ILogger<SemanticTokensHandler> logger)
        {
            _logger = logger;
        }

        public override async Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public override async Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public override async Task<SemanticTokensFullOrDelta?> Handle(SemanticTokensDeltaParams request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
            return result;
        }

        //protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
        //    CancellationToken cancellationToken)
        //{
        //    using var typesEnumerator = RotateEnum(SemanticTokenType.Defaults).GetEnumerator();
        //    using var modifiersEnumerator = RotateEnum(SemanticTokenModifier.Defaults).GetEnumerator();
        //    // you would normally get this from a common source that is managed by current open editor, current active editor, etc.
        //    var content = await File.ReadAllTextAsync(DocumentUri.GetFileSystemPath(identifier), cancellationToken).ConfigureAwait(false);
        //    await Task.Yield();

        //    foreach (var (line, text) in content.Split('\n').Select((text, line) => (line, text)))
        //    {
        //        var parts = text.TrimEnd().Split(';', ' ', '.', '"', '(', ')');
        //        var index = 0;
        //        foreach (var part in parts)
        //        {
        //            typesEnumerator.MoveNext();
        //            modifiersEnumerator.MoveNext();
        //            if (string.IsNullOrWhiteSpace(part)) continue;
        //            index = text.IndexOf(part, index, StringComparison.Ordinal);
        //            builder.Push(line, index, part.Length, typesEnumerator.Current, modifiersEnumerator.Current);
        //        }
        //    }
        //}

        protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
            CancellationToken cancellationToken)
        {
            // 1. Читаем текст файла
            var filePath = DocumentUri.GetFileSystemPath(identifier);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            await Task.Yield();

            // 2. Создаем потоки ANTLR для вашего лексера (предположим, ваш лексер называется CevolLexer)
            //var inputStream = Antlr4.Runtime.CharStreams.fromString(content);
            //var lexer = new CevolLexer(inputStream);
            //var tokens = lexer.GetAllTokens();

            ICharStream stream = CharStreams.fromString(content);
            var lexer = new CEvolLexer(stream);

            // 3. Проходим по всем токенам от ANTLR и передаем их в builder
            foreach (var token in lexer.GetAllTokens())
            {
                // ANTLR считает строки с 1, а LSP с 0 -> поэтому line - 1
                var line = token.Line - 1;
                var column = token.Column;
                var length = token.Text.Length;

                // Превращаем тип токена ANTLR в индекс типа из легенды LSP
                int tokenTypeIndex = MapAntlrTokenTypeToLsp(token.Type);

                // Передаем токен в редактор
                builder.Push(line, column, length, tokenTypeIndex, 0);
            }
        }

        // Метод маппинга типов токенов ANTLR на индексы в легенде SemanticTokens
        private int MapAntlrTokenTypeToLsp(int antlrTokenType)
        {
            // Здесь вы сопоставляете константы из CevolLexer (например, CevolLexer.KEYWORD, CevolLexer.IDENTIFIER)
            // с числовыми индексами типов в вашей легенде (например, 0 - keyword, 1 - variable и т.д.)
            return antlrTokenType switch
            {
                CEvolLexer.STRING => 1,
                CEvolLexer.LINE_COMMENT => 4,
                CEvolLexer.NUMBER => 3,
                CEvolLexer.IDENTIFIER => 2,
                _ => 0
            };
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
        }


        private IEnumerable<T> RotateEnum<T>(IEnumerable<T> values)
        {
            while (true)
            {
                foreach (var item in values)
                    yield return item;
            }
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability,
            ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = TextDocumentSelector.ForLanguage("cevol"),
                Legend = new SemanticTokensLegend
                {
                    // 1. Определяем ваш собственный фиксированный список типов токенов.
                    // Порядок элементов здесь СТРОГО должен соответствовать индексам, 
                    // которые возвращает ваш метод MapAntlrTokenTypeToLsp!
                    TokenTypes = new Container<SemanticTokenType>(
                        SemanticTokenType.Keyword, // 0
                        SemanticTokenType.String, // 1 ++
                        SemanticTokenType.Variable, // 2
                        SemanticTokenType.Number, // 3 ++
                        SemanticTokenType.Comment // 4 ++
                    ),

                    // 2. Модификаторы токенов (можно оставить стандартные из capabilities или задать свои)
                    TokenModifiers = capability.TokenModifiers ?? new Container<SemanticTokenModifier>()
                },
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = true
                },
                Range = true
            };
        }

        //protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability,
        //    ClientCapabilities clientCapabilities)
        //{
        //    return new SemanticTokensRegistrationOptions
        //    {
        //        DocumentSelector = TextDocumentSelector.ForLanguage("cevol"),
        //        Legend = new SemanticTokensLegend
        //        {
        //            TokenModifiers = capability.TokenModifiers,
        //            TokenTypes = capability.TokenTypes
        //        },
        //        Full = new SemanticTokensCapabilityRequestFull
        //        {
        //            Delta = true
        //        },
        //        Range = true
        //    };
        //}
    }
}
