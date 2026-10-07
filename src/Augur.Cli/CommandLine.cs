using System.CommandLine;
using System.CommandLine.Invocation;
using Augur.Cli.Commands;

namespace Augur.Cli;

/// <summary>Builds the augur command tree and turns parser errors into single <c>error:</c> lines.</summary>
internal static class CommandLine
{
    public static ParserConfiguration ParserConfiguration => new() { ResponseFileTokenReplacer = null };

    public static InvocationConfiguration Invocation(TextWriter output, TextWriter error) => new()
    {
        Output = output,
        Error = error,
        ProcessTerminationTimeout = null,
        EnableDefaultExceptionHandler = false,
    };

    public static RootCommand Build(CliContext context)
    {
        var root = new RootCommand("Augur decides what to generate with the OpenAI Decisions API, then emits code deterministically.");
        root.Directives.Clear();
        foreach (var version in root.Options.OfType<VersionOption>())
        {
            version.Action = new VersionAction();
        }

        context.Verbosity = CliOptions.Verbosity();
        root.Options.Add(context.Verbosity);
        root.Subcommands.Add(new PlanCommand(context));
        root.Subcommands.Add(new EmitCommand(context));
        root.Subcommands.Add(new GenerateCommand(context));
        root.Subcommands.Add(new ExplainCommand(context));
        root.Subcommands.Add(new CatalogCommand(context));
        root.Subcommands.Add(new SchemaCommand(context));
        root.SetAction(_ =>
        {
            context.WriteHelp([], context.Host.Stderr);
            return 2;
        });
        context.Root = root;
        return root;
    }

    public static async Task<int> RunAsync(ConsoleHost host)
    {
        var context = new CliContext(host);
        var root = Build(context);
        var parseResult = root.Parse(host.Args, ParserConfiguration);
        if (parseResult.Action is ParseErrorAction)
        {
            host.Stderr.WriteLine($"error: {DescribeParseError(parseResult, root)}");
            return 2;
        }

        return await parseResult.InvokeAsync(Invocation(host.Stdout, host.Stderr), host.Token);
    }

    private static string DescribeParseError(ParseResult parseResult, RootCommand root)
    {
        var unmatched = parseResult.UnmatchedTokens.FirstOrDefault();
        if (unmatched is not null)
        {
            if (unmatched.StartsWith('-'))
            {
                return $"Unknown option '{unmatched}'";
            }

            return parseResult.CommandResult.Command == root
                ? $"Unknown command '{unmatched}'"
                : $"Unexpected argument '{unmatched}'";
        }

        return parseResult.Errors[0].Message;
    }
}
