using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using PromptSampleTests.Commands;

namespace PromptSampleTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        
        app.Configure(config =>
        {
            config.SetApplicationName("PromptSampleTests");
            config.SetApplicationVersion("1.0.0");
            
            config.AddCommand<FileCommand>("file")
                .WithDescription("Test prompts using files from a directory")
                .WithExample("file", "gpt-4o-2024-08-06", "c:\\path\\to\\2425_md34_rbl_vfb");
                
            config.AddCommand<LiveCommand>("live")
                .WithDescription("Test prompts using live Kicktipp data and instructions template")
                .WithExample("live", "o4-mini")
                .WithExample("live", "o4-mini", "--home", "VfB Stuttgart", "--away", "Borussia Dortmund");
        });

        return await app.RunAsync(args);
    }
}
