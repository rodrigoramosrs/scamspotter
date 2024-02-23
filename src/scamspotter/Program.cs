using Figgle;
using Microsoft.Extensions.DependencyInjection;
using ScamSpotter.Commands;
using ScamSpotter.Infrastructure;
using ScamSpotter.Services;
using ScamSpotter.Services.OSINT;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;

namespace ScamSpotter
{
    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            PrintHeader();
            AnsiConsole.MarkupLine("[green bold][[ {0}[/] [green]- v.[/] [green bold]{1} ]][/]", Markup.Escape("ScamSpotter"), Markup.Escape($"{typeof(Program).Assembly.GetName().Version}"));

            AnsiConsole.MarkupLine("[green bold][[ Github:[/] [green][link]https://github.com/rodrigoramosrs/scamspotter[/] ]][/]");

            var serviceCollection = new ServiceCollection()
            .AddLogging(configure =>
                configure.AddSerilog(new LoggerConfiguration()
                    // log level will be dynamically be controlled by our log interceptor upon running
                    .MinimumLevel.ControlledBy(LogInterceptor.LogLevel)
                    // the log enricher will add a new property with the log file path from the settings
                    // that we can use to set the path dynamically
                    .Enrich.With<LoggingEnricher>()
                    // serilog.sinks.map will defer the configuration of the sink to be ondemand
                    // allowing us to look at the properties set by the enricher to set the path appropriately
                    .WriteTo.Map(LoggingEnricher.LogFilePathPropertyName,
                        (logFilePath, wt) => wt.File($"{logFilePath}"), 1)
                    .CreateLogger()
                )
            );
            serviceCollection.AddTransient(typeof(WhoIsService));
            serviceCollection.AddSingleton(typeof(Services.ScreenshotService));
            serviceCollection.AddTransient(typeof(Services.RequestService));
            serviceCollection.AddTransient(typeof(CrtShService));
            serviceCollection.AddTransient(typeof(ScamDetectService));

            var registrar = new TypeRegistrar(serviceCollection);
            var app = new CommandApp(registrar);

            app.Configure(config =>
            {
                config.SetInterceptor(new LogInterceptor()); // add the interceptor
                config.AddCommand<AppCommand>("watch");
            });

            //TODO: VERIFICAR PROPAGANDAS TAMBÉM NO ADS DO GOOGLE, BING E ETC.. BUSCANDO SITES QUE POSSAM SER FALSOS E ESTÃO SENDO ANUNCIADOS

            return Task.FromResult(app.Run(args));
        }

        private static void PrintHeader()
        {
            PropertyInfo[] propertyInfoArray = typeof(FiggleFonts).GetProperties();
            int randomNumber = new Random().Next(0, propertyInfoArray.Count() - 1);

            var propertyInfo = propertyInfoArray[randomNumber];

            object propertyValue = propertyInfo.GetValue(null);

            // Get the MethodInfo object for the 'Render' method
            MethodInfo renderMethod = propertyValue.GetType().GetMethod("Render");

            // Invoke the 'Render' method
            string result = (string)renderMethod.Invoke(propertyValue, new object[] { "ScammSpotter", 0 });

            AnsiConsole.MarkupLine("[green]{0}[/]", Markup.Escape(result));

        }
    }
}