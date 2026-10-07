using System.CommandLine;
using Augur.Core.Plan;

namespace Augur.Cli.Commands;

/// <summary>Factories for the options shared by several commands. Each command gets its own instances.</summary>
internal static class CliOptions
{
    public const string DefaultLockPath = "decisions.json";
    public const string DefaultModel = "gpt-6-luna";
    public const int DefaultTimeoutSeconds = 30;

    public static Option<string> Verbosity() => new Option<string>("--verbosity")
    {
        Description = "How much to write to stderr: quiet, normal, detailed, or diagnostic.",
        DefaultValueFactory = _ => "normal",
        Recursive = true,
        HelpName = "quiet|normal|detailed|diagnostic",
    }.AcceptOnlyFromAmong("quiet", "normal", "detailed", "diagnostic");

    public static Option<string> Spec() => new("--spec")
    {
        Description = "Path to the specification (UTF-8 text, up to 256 KiB). Use - to read it from stdin.",
        Required = true,
        HelpName = "path",
    };

    public const int MaxImages = 4;

    public static Option<string[]> Image()
    {
        var option = new Option<string[]>("--image")
        {
            Description = "PNG, JPEG, or WEBP image to send with the specification. Repeat up to 4 times.",
            HelpName = "path",
            Arity = ArgumentArity.OneOrMore,
        };
        option.Validators.Add(result =>
        {
            if (result.Tokens.Count > MaxImages)
            {
                result.AddError($"at most {MaxImages} images are allowed");
            }
        });
        return option;
    }

    public static Option<string> Name()
    {
        var option = new Option<string>("--name")
        {
            Description = "Name of the generated solution, for example Contoso.Orders.",
            Required = true,
            HelpName = "SolutionName",
        };
        option.Validators.Add(result =>
        {
            if (!SolutionName.TryParse(result.GetValueOrDefault<string>(), out _))
            {
                result.AddError(SolutionName.Rule);
            }
        });
        return option;
    }

    public static Option<string[]> Set() => new("--set")
    {
        Description = "Fix a decision instead of asking for it, as <id>=<value>. Repeatable.",
        HelpName = "id=value",
        Arity = ArgumentArity.OneOrMore,
    };

    public static Option<string> Lock() => new("--lock")
    {
        Description = "Path to the decisions lockfile.",
        DefaultValueFactory = _ => DefaultLockPath,
        HelpName = "path",
    };

    public static Option<string[]> Refresh() => new("--refresh")
    {
        Description = "Ignore recorded decisions and ask again: all of them, or only the named decision ids.",
        HelpName = "id",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static Option<bool> Offline() => new("--offline")
    {
        Description = "Forbid network access; resolve decisions only from overrides and the lockfile.",
    };

    public static Option<string> OracleScript() => new("--oracle-script")
    {
        Description = "Take answers from a JSON answer file instead of the Decisions API.",
        HelpName = "path",
    };

    public static Option<string> OnLowConfidence() => new Option<string>("--on-low-confidence")
    {
        Description = "How to resolve low-confidence decisions: default, prompt, or fail. Defaults to prompt in a terminal and fail otherwise.",
        HelpName = "default|prompt|fail",
    }.AcceptOnlyFromAmong("default", "prompt", "fail");

    public static Option<double?> MinConfidence()
    {
        var option = new Option<double?>("--min-confidence")
        {
            Description = "Minimum confidence (0 to 1) for choice and score decisions, replacing the catalog value.",
            HelpName = "value",
        };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<double?>() is { } value && (value < 0 || value > 1 || double.IsNaN(value)))
            {
                result.AddError("--min-confidence must be between 0 and 1");
            }
        });
        return option;
    }

    public static Option<string> Model() => new("--model")
    {
        Description = "Decisions API model.",
        DefaultValueFactory = _ => DefaultModel,
        HelpName = "model",
    };

    public static Option<int> Timeout()
    {
        var option = new Option<int>("--timeout")
        {
            Description = "Per-request timeout for the Decisions API, in seconds (1 to 300).",
            DefaultValueFactory = _ => DefaultTimeoutSeconds,
            HelpName = "seconds",
        };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 300)
            {
                result.AddError("--timeout must be between 1 and 300 seconds");
            }
        });
        return option;
    }

    public static Option<string> OutFile() => new("--out")
    {
        Description = "Write the plan to this file instead of stdout.",
        HelpName = "path",
    };

    public static Option<string> OutDirectory() => new("--out")
    {
        Description = "Directory to emit the code into.",
        Required = true,
        HelpName = "dir",
    };

    public static Option<string> Plan() => new("--plan")
    {
        Description = "Path to the GenerationPlan to emit.",
        Required = true,
        HelpName = "path",
    };

    public static Option<bool> Force() => new("--force")
    {
        Description = "Allow emitting into a non-empty directory. Only files Augur emits are overwritten.",
    };

    public static Option<bool> DryRun() => new("--dry-run")
    {
        Description = "List the files that would be written, without writing anything.",
    };

    public static Option<bool> Json() => new("--json")
    {
        Description = "Write the output as JSON.",
    };
}
