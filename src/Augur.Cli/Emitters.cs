using Augur.Core.Catalog;
using Augur.Emission;
using Augur.Emission.DotNet;

namespace Augur.Cli;

/// <summary>The emitters augur ships, composed in a fixed order.</summary>
internal static class Emitters
{
    public static IEmitter Create(ConsoleHost host)
    {
        IEmitter emitter = new CompositeEmitter(
        [
            new ReadmeEmitter(DecisionCatalog.BuiltIn),
            new DotNetSolutionEmitter(),
        ]);
        return host.EmitterDecorator?.Invoke(emitter) ?? emitter;
    }
}
