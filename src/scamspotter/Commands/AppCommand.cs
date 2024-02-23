using Microsoft.Extensions.Logging;
using ScamSpotter.Global;
using ScamSpotter.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace ScamSpotter.Commands;

public class AppCommand : Command<AppCommand.Settings>
{
    private ILogger<AppCommand> _logger;
    private IAnsiConsole _console;
    private ScamDetectService _scamDetectService;

    public AppCommand(IAnsiConsole console, ILogger<AppCommand> logger, ScamDetectService scamDetectService)
    {
        _console = console;
        _logger = logger;
        _logger.LogDebug("{0} initialized", nameof(AppCommand));
        _scamDetectService = scamDetectService;
    }

    public class Settings : LogCommandSettings
    {
        [CommandOption("-t|--terms")]
        [Description("Path to terms file. ")]
        public string Terms { get; set; }

        [CommandOption("-s|--screenshot")]
        [Description("Take screenshot from domain found.")]
        public bool SaveScreenshot { get; set; }

        [CommandOption("--poolsize")]
        [Description("Set pool size for thread creation.")]
        public int MaximumPoolSize { get; set; } = 10;
    }


    public override int Execute(CommandContext context, Settings settings)
    {

        _logger.LogInformation("Starting my command");

        if (!File.Exists(settings.Terms))
        {
            AnsiConsole.MarkupLine("The 'Terms file' was not found.");
            return 1;
        }

        Global.GlobalSettings.MaximumPoolSize = settings.MaximumPoolSize > 10 ? settings.MaximumPoolSize : 10;
        Global.GlobalSettings.SaveScreenshot = settings.SaveScreenshot;
        Global.GlobalSettings.TermsFullFilePath = settings.Terms;

        AnsiConsole.MarkupLine($"- - - - - - - - - - - - - - - - - - - - - - - - - - - - ");
        AnsiConsole.MarkupLine($"Screenshot: {Global.GlobalSettings.SaveScreenshot}");
        AnsiConsole.MarkupLine($"Terms: {Global.GlobalSettings.TermsFullFilePath}");
        AnsiConsole.MarkupLine($"Output path: {Global.GlobalSettings.OutputRootDirectory.Replace(GlobalSettings.RootPath, "./")}");
        AnsiConsole.MarkupLine($"PoolSize : {Global.GlobalSettings.MaximumPoolSize}");
        AnsiConsole.MarkupLine($"- - - - - - - - - - - - - - - - - - - - - - - - - - - - ");
        _scamDetectService.StartDetection().GetAwaiter().GetResult();

        _logger.LogInformation("Completed my command");

        return 0;
    }

}
