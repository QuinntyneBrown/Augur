using System.Text.Json.Nodes;
using Augur.Core;
using Augur.Core.Catalog;

namespace Augur.Tests.Catalog;

public sealed class QuestionHashTests
{
    private const string SnapshotFile = "Catalog/question-hashes.v2.json";

    [Fact]
    public void Changing_one_option_description_changes_only_that_decisions_hash()
    {
        var original = CatalogVersion2.Build();
        var changed = original
            .Select(d => d is ChoiceDefinition { Id: "persistence" } c
                ? c with { Options = [.. c.Options.Select(o => o.Value == "none" ? o with { Description = o.Description + " Changed." } : o)] }
                : d)
            .ToList();

        var differing = original.Zip(changed)
            .Where(pair => QuestionHasher.Hash(pair.First) != QuestionHasher.Hash(pair.Second))
            .Select(pair => pair.First.Id);

        Assert.Equal(["persistence"], differing);
    }

    [Fact]
    public void The_hash_ignores_id_default_and_when_but_covers_thresholds()
    {
        var architecture = (ChoiceDefinition)CatalogVersion2.Build().Single(d => d.Id == "architecture");
        var hash = QuestionHasher.Hash(architecture);

        Assert.Equal(hash, QuestionHasher.Hash(architecture with { Id = "renamed", Default = "minimal-api", When = null }));
        Assert.NotEqual(hash, QuestionHasher.Hash(architecture with { MinConfidence = 0.71 }));
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    /// <summary>
    /// Any change to a question changes its hash, which must come with a catalog version bump (L2-009).
    /// When this fails after an intended catalog change: increment the catalog version, then update the snapshot.
    /// </summary>
    [Fact]
    public void The_shipped_question_hashes_match_the_snapshot_for_this_catalog_version()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SnapshotFile);
        var expected = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
        var actual = CanonicalJson.Serialize(new JsonObject
        {
            ["catalogVersion"] = DecisionCatalog.BuiltIn.Version,
            ["questionHashes"] = new JsonObject(DecisionCatalog.BuiltIn.Decisions
                .Select(d => KeyValuePair.Create(d.Id, (JsonNode?)d.QuestionHash))),
        });

        Assert.True(
            expected == actual,
            $"The catalog's question hashes differ from tests/Augur.Tests/{SnapshotFile}. "
            + "If the catalog change is intended, bump the catalog version, then replace the snapshot with:\n" + actual);
    }
}
