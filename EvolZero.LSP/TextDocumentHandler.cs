using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using EvolZero.Core;
using EvolZero.Core.Analysis;
using EvolZero.Core.Analysis.Semantic;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Parsing;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EvolZero.LSP
{
    internal class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        //public static ConcurrentDictionary<DocumentUri, List<Diagnostic>> Contents = new(); // TODO: передать через DI а не статикой
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly AnalysisService _analysisService;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServerFacade _languageServerFacade;

        //private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
        //    new TextDocumentFilter
        //    {
        //        Pattern = "**/*.cev"
        //    }
        //);

        private readonly TextDocumentSelector _textDocumentSelector = TextDocumentSelector.ForLanguage("cevol");

        public TextDocumentHandler(ILogger<TextDocumentHandler> logger, Foo foo, AnalysisService analysisService,
            ILanguageServerConfiguration configuration, ILanguageServerFacade languageServerFacade)
        {
            _logger = logger;
            _analysisService = analysisService;
            _configuration = configuration;
            _languageServerFacade = languageServerFacade;
            foo.SayFoo();
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogCritical("Critical");
            _logger.LogDebug("Debug");
            _logger.LogTrace("Trace");
            _logger.LogInformation("Hello world!");
            return Unit.Task;
        }

        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            //await Task.Yield();
            //_logger.LogInformation("Hello world!");
            //await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);
            //return Unit.Value;

            await Task.Yield();
            _logger.LogInformation("Файл открыт: {Uri}", notification.TextDocument.Uri);

            // Пример отправки фиктивной ошибки в первую строчку файла
            //var diagnostics = new List<Diagnostic>
            //{
            //    new Diagnostic
            //    {
            //        Range = new Range(new Position(0, 0), new Position(0, 5)),
            //        Severity = DiagnosticSeverity.Error,
            //        Message = "Пример ошибки от вашего LSP-сервера!",
            //        Source = "MyCustomLang"
            //    }
            //};

            var diagnostics =  _analysisService.FileChanged(notification.TextDocument.Uri, notification.TextDocument.Text);


            //var diagnostics = errors.Select(x => new Diagnostic()
            //{
            //    Range = new Range(new Position(x.Pos.Line - 1, x.Pos.Symbol), new Position(x.Pos.Line - 1, x.Pos.EndSymbol)),
            //    Severity = DiagnosticSeverity.Error,
            //    Message = $"{x.ErrorCode} [{x.Layer}]: {x.Comment}",
            //    Source = "MyCustomLang"
            //}).ToList();

            //Contents[notification.TextDocument.Uri] = notification.TextDocument;

            string fileContet = notification.TextDocument.Text;

            _languageServerFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = notification.TextDocument.Uri,
                Diagnostics = diagnostics
            });

            return Unit.Value;
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
            {
                disposable.Dispose();
            }

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _textDocumentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, "cevol");
    }
}
