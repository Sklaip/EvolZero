using Antlr4.Runtime;
using EvolZero.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmmniSemanticTokenType = OmniSharp.Extensions.LanguageServer.Protocol.Models.SemanticTokenType;
using SemanticTokenType = EvolZero.Core.SemanticTokenType;


namespace EvolZero.LSP
{
    public class SemanticTokensHandler : SemanticTokensHandlerBase
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;

        public SemanticTokensHandler(ILogger<SemanticTokensHandler> logger, AnalysisService analysisService)
        {
            _logger = logger;
            _analysisService = analysisService;
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
            await Task.Yield();

            var positions = _analysisService.GetPositions(identifier.TextDocument.Uri);

            foreach (var pos in positions)
            {
                //var tokenType = MapTokenType(pos.tokenType);
                builder.Push(pos.pos.Line - 1, pos.pos.Symbol, pos.pos.EndSymbol - pos.pos.Symbol + 1, (int)pos.tokenType, 0);

                if(pos.pos.Line == 146)
                {
                    Console.WriteLine($"{pos.pos.Line}, {pos.pos.Symbol}, {pos.pos.EndSymbol}, {pos.tokenType}");
                }
            }

        }

        // Метод маппинга типов токенов ANTLR на индексы в легенде SemanticTokens
        private OmmniSemanticTokenType MapTokenType(SemanticTokenType tokenType)
        {
            // Здесь вы сопоставляете константы из CevolLexer (например, CevolLexer.KEYWORD, CevolLexer.IDENTIFIER)
            // с числовыми индексами типов в вашей легенде (например, 0 - keyword, 1 - variable и т.д.)
            return tokenType switch
            {
                SemanticTokenType.Comment => OmmniSemanticTokenType.Comment,
                SemanticTokenType.Keyword => OmmniSemanticTokenType.Keyword,
                SemanticTokenType.String => OmmniSemanticTokenType.String,
                SemanticTokenType.Number => OmmniSemanticTokenType.Number,
                SemanticTokenType.Regexp => OmmniSemanticTokenType.Regexp,
                SemanticTokenType.Operator => OmmniSemanticTokenType.Operator,
                SemanticTokenType.Namespace => OmmniSemanticTokenType.Namespace,
                SemanticTokenType.Type => OmmniSemanticTokenType.Type,
                SemanticTokenType.Struct => OmmniSemanticTokenType.Struct,
                SemanticTokenType.Class => OmmniSemanticTokenType.Class,
                SemanticTokenType.Interface => OmmniSemanticTokenType.Interface,
                SemanticTokenType.Enum => OmmniSemanticTokenType.Enum,
                SemanticTokenType.TypeParameter => OmmniSemanticTokenType.TypeParameter,
                SemanticTokenType.Function => OmmniSemanticTokenType.Function,
                SemanticTokenType.Method => OmmniSemanticTokenType.Method,
                SemanticTokenType.Property => OmmniSemanticTokenType.Property,
                SemanticTokenType.Macro => OmmniSemanticTokenType.Macro,
                SemanticTokenType.Variable => OmmniSemanticTokenType.Variable,
                SemanticTokenType.Parameter => OmmniSemanticTokenType.Parameter,
                SemanticTokenType.Label => OmmniSemanticTokenType.Label,
                SemanticTokenType.Modifier => OmmniSemanticTokenType.Modifier,
                SemanticTokenType.Event => OmmniSemanticTokenType.Event,
                SemanticTokenType.EnumMember => OmmniSemanticTokenType.EnumMember,
                _ => OmmniSemanticTokenType.Keyword
            }; ;
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

            //return new SemanticTokensRegistrationOptions
            //{
            //    DocumentSelector = TextDocumentSelector.ForLanguage("cevol"),
            //    Legend = new SemanticTokensLegend
            //    {
            //        TokenModifiers = capability.TokenModifiers,
            //        TokenTypes = capability.TokenTypes
            //    },
            //    Full = new SemanticTokensCapabilityRequestFull
            //    {
            //        Delta = true
            //    },
            //    Range = true
            //};

            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = TextDocumentSelector.ForLanguage("cevol"),
                Legend = new SemanticTokensLegend
                {
                    TokenTypes = new Container<OmmniSemanticTokenType>(
                        OmmniSemanticTokenType.Comment,
                        OmmniSemanticTokenType.Keyword,
                          OmmniSemanticTokenType.String,
                          OmmniSemanticTokenType.Number,
                          OmmniSemanticTokenType.Regexp,
                          OmmniSemanticTokenType.Operator,
                          OmmniSemanticTokenType.Namespace,
                          OmmniSemanticTokenType.Type,
                          OmmniSemanticTokenType.Struct,
                          OmmniSemanticTokenType.Class,
                         OmmniSemanticTokenType.Interface,
                         OmmniSemanticTokenType.Enum,
                          OmmniSemanticTokenType.TypeParameter,
                          OmmniSemanticTokenType.Function,
                          OmmniSemanticTokenType.Method,
                          OmmniSemanticTokenType.Property,
                          OmmniSemanticTokenType.Macro,
                          OmmniSemanticTokenType.Variable,
                          OmmniSemanticTokenType.Parameter,
                          OmmniSemanticTokenType.Label,
                          OmmniSemanticTokenType.Modifier,
                          OmmniSemanticTokenType.Event,
                          OmmniSemanticTokenType.EnumMember,
                          new OmmniSemanticTokenType("field"),
                          new OmmniSemanticTokenType("punctuation")
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
    }
}
