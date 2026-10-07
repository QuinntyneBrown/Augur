using System.Collections.Concurrent;
using System.Reflection;
using Scriban;
using Scriban.Runtime;

namespace Augur.Emission;

/// <summary>
/// Renders Scriban templates embedded in an emitter assembly. Templates see only the model they are given: there is
/// no template loader, and the date built-ins are removed, so output cannot depend on the clock, the environment,
/// or the file system.
/// </summary>
public sealed class TemplateEngine(Assembly assembly)
{
    private static readonly ConcurrentDictionary<(Assembly, string), Template> Parsed = new();

    /// <summary>Renders the template at <paramref name="name"/> (its path under <c>Templates/</c>) with <paramref name="model"/> as globals.</summary>
    public string Render(string name, object model)
    {
        var template = Parsed.GetOrAdd((assembly, name), key =>
        {
            var parsed = Template.Parse(ReadText(key.Item2), key.Item2);
            if (parsed.HasErrors)
            {
                throw new InvalidOperationException($"template {key.Item2} is invalid: {string.Join("; ", parsed.Messages)}");
            }

            return parsed;
        });

        var builtins = TemplateContext.GetDefaultBuiltinObject();
        builtins.Remove("date");
        builtins.Remove("timespan");
        var globals = new ScriptObject();
        globals.Import(model, renamer: member => member.Name);
        var context = new TemplateContext(builtins)
        {
            MemberRenamer = member => member.Name,
            StrictVariables = true,
            TemplateLoader = null,
            LoopLimit = 10_000,
            NewLine = "\n",
        };
        context.PushGlobal(globals);
        return template.Render(context);
    }

    /// <summary>A file embedded as-is, such as a lockfile or an icon.</summary>
    public byte[] Static(string name)
    {
        using var stream = Open(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private string ReadText(string name)
    {
        using var reader = new StreamReader(Open(name));
        return reader.ReadToEnd();
    }

    private Stream Open(string name) =>
        assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"{assembly.GetName().Name} has no embedded template {name}");
}
