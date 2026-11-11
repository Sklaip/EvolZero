using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace EvolZero.LSP
{
    internal class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly AnalysisService _analysisService;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServerFacade _languageServerFacade;
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

        public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            var changes = notification.ContentChanges.LastOrDefault();
            if (changes?.Text == null) return Unit.Value;

            await Task.Yield();

            var diagnostics = await _analysisService.FileChanged(notification.TextDocument.Uri, changes.Text);

            _languageServerFacade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = notification.TextDocument.Uri,
                Diagnostics = diagnostics
            });

            return Unit.Value;
        }

        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();

            var diagnostics = await _analysisService.FileChanged(notification.TextDocument.Uri, notification.TextDocument.Text);

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
