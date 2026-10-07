using Augur.Core.Catalog;
using Augur.Emission;

namespace Augur.Cli;

/// <summary>The emitters augur ships, composed in a fixed order.</summary>
internal static class Emitters
{
    public static IEmitter Create(ConsoleHost host)
    {
        IEmitter emitter = new CompositeEmitter([new ReadmeEmitter(DecisionCatalog.BuiltIn)]);
        return host.EmitterDecorator?.Invoke(emitter) ?? emitter;
    }
}
