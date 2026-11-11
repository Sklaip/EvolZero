using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using System.Net;
using System.Net.Sockets;

namespace EvolZero.LSP
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Debugger.Launch();
            // while (!Debugger.IsAttached)
            // {
            //     await Task.Delay(100);
            // }

            //Log.Logger = new LoggerConfiguration()
            //            .Enrich.FromLogContext()
            //            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            //            .MinimumLevel.Verbose()
            //            .CreateLogger();

            //Log.Logger.Information("This only goes file...");

            IObserver<WorkDoneProgressReport> workDone = null!;

            var listener = new TcpListener(IPAddress.Loopback, 5007); // Выберите свободный порт, например 5007
            listener.Start();
            Console.WriteLine("Ожидание подключения LSP-клиента на порту 5007...");

            var clientStream = listener.AcceptTcpClient().GetStream();

            var server = await LanguageServer.From(
                options =>
                    options
                       //.WithInput(Console.OpenStandardInput())
                       //.WithOutput(Console.OpenStandardOutput())
                       .WithInput(clientStream)
                       .WithOutput(clientStream)
                       .ConfigureLogging(
                            x => x
                                //.AddSerilog(Log.Logger)
                                .AddLanguageProtocolLogging()
                                .SetMinimumLevel(LogLevel.Debug)
                        )
                       .WithHandler<TextDocumentHandler>()
                       .WithHandler<DidChangeWatchedFilesHandler>()
                       .WithHandler<FoldingRangeHandler>()
                       .WithHandler<WorkspaceSymbolsHandler>()
                       .WithHandler<MyDocumentSymbolHandler>()
                       .WithHandler<SemanticTokensHandler>()
                       .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
                       .WithServices(
                            services =>
                            {
                                services.AddSingleton(
                                    new ConfigurationItem
                                    {
                                        Section = "typescript",
                                    }
                                ).AddSingleton(
                                    new ConfigurationItem
                                    {
                                        Section = "terminal",
                                    }
                                );
                                services.AddSingleton<AnalysisService>();
                            }
                        )
                       .OnInitialize(
                            async (server, request, token) =>
                            {
                                var manager = server.WorkDoneManager.For(
                                    request, new WorkDoneProgressBegin
                                    {
                                        Title = "Server is starting...",
                                        Percentage = 10,
                                    }
                                );
                                workDone = manager;

                                //await Task.Delay(2000).ConfigureAwait(false);

                                manager.OnNext(
                                    new WorkDoneProgressReport
                                    {
                                        Percentage = 20,
                                        Message = "loading in progress"
                                    }
                                );
                            }
                        )
                       .OnInitialize(async (server, request, token) =>
                       {
                           var rootPath = request.RootPath;

                           if (!string.IsNullOrEmpty(rootPath))
                           {
                               var analysisService = server.Services.GetService<AnalysisService>();
                               await analysisService!.InitializeProject(rootPath);
                           }
                       })
            ).ConfigureAwait(false);

            await server.WaitForExit.ConfigureAwait(false);
        }
    }
}
