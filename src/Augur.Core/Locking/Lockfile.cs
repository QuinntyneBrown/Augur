using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augur.Core.Catalog;
using Augur.Core.Decisions;

namespace Augur.Core.Locking;

/// <summary>Every decision resolved for one input, so a re-run can replay them instead of asking again.</summary>
public sealed record Lockfile(int CatalogVersion, string InputHash, IReadOnlyList<LockfileEntry> Entries)
{
    public const int CurrentVersion = 1;

    public LockfileEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Builds the lockfile for a finished evaluation. Overridden and skipped decisions are not recorded.</summary>
    public static Lockfile From(DecisionState state, string inputHash, DecisionCatalog catalog, ResolverOptions options) =>
        new(
            catalog.Version,
            inputHash,
            [.. state.Resolved
                .Where(d => d.Source != DecisionSource.Override && d.Raw is not null)
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .Select(d => new LockfileEntry(
                    d.Id,
                    d.QuestionHash,
                    d.Raw!.Replayed?.OriginalSource ?? d.Source,
                    d.Value,
                    d.Raw,
                    d.Raw.Model,
                    d.ResolvedAt,
                    catalog.TryGet(d.Id, out var definition) ? Thresholds.Of(definition, options) : null))]);
}

/// <summary>One recorded decision: its value, its source, and the raw result it was resolved from.</summary>
public sealed record LockfileEntry(
    string Id,
    string QuestionHash,
    DecisionSource Source,
    string Value,
    DecisionAnswer Raw,
    string? Model,
    DateTimeOffset ResolvedAt,
    Thresholds? Thresholds);

/// <summary>The thresholds that applied when a decision was resolved, recorded so <c>augur explain</c> can show them.</summary>
public sealed record Thresholds(double? Upper, double? Lower, double? MinConfidence, double? CutOff)
{
    public static Thresholds Of(DecisionDefinition definition, ResolverOptions options) => definition switch
    {
        PredicateDefinition p => new(p.UpperThreshold, p.LowerThreshold, null, null),
        ChoiceDefinition c => new(null, null, options.MinConfidenceOverride ?? c.MinConfidence, null),
        ScoreDefinition s => new(null, null, options.MinConfidenceOverride ?? s.MinConfidence, s.CutOff),
        _ => new(null, null, null, null),
    };

    public JsonObject ToJson()
    {
        var json = new JsonObject();
        if (Upper is { } upper)
        {
            json["upper"] = upper;
        }

        if (Lower is { } lower)
        {
            json["lower"] = lower;
        }

        if (MinConfidence is { } minConfidence)
        {
            json["minConfidence"] = minConfidence;
        }

        if (CutOff is { } cutOff)
        {
            json["cutOff"] = cutOff;
        }

        return json;
    }

    public static Thresholds? FromJson(JsonNode? node) => node is JsonObject json
        ? new(Number(json, "upper"), Number(json, "lower"), Number(json, "minConfidence"), Number(json, "cutOff"))
        : null;

    private static double? Number(JsonObject json, string name) =>
        json[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;
}

/// <summary>Reads and writes the lockfile format: entries sorted by id, canonical JSON.</summary>
public static class LockfileSerializer
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static string Serialize(Lockfile lockfile, DecisionCatalog catalog) => CanonicalJson.Serialize(new JsonObject
    {
        ["lockfileVersion"] = Lockfile.CurrentVersion,
        ["catalogVersion"] = lockfile.CatalogVersion,
        ["inputHash"] = lockfile.InputHash,
        ["entries"] = new JsonArray([.. lockfile.Entries.OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => ToJson(e, catalog))]),
    });

    /// <exception cref="UsageException">The content is not a supported lockfile.</exception>
    public static Lockfile Deserialize(string json, string displayPath, DecisionCatalog catalog)
    {
        JsonNode? root;
        try
        {
            root = CanonicalJson.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new UsageException(JsonErrors.Describe(displayPath, ex), ex);
        }

        if (root is not JsonObject obj)
        {
            throw new UsageException($"{displayPath} is not a lockfile: expected a JSON object");
        }

        var version = obj["lockfileVersion"] is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>() : double.NaN;
        if (version != Lockfile.CurrentVersion)
        {
            throw new UsageException(
                $"{displayPath} has unsupported lockfileVersion {obj["lockfileVersion"]?.ToJsonString() ?? "(missing)"}; this version of augur reads version {Lockfile.CurrentVersion}");
        }

        var inputHash = obj["inputHash"] is JsonValue h && h.GetValueKind() == JsonValueKind.String
            ? h.GetValue<string>()
            : throw new UsageException($"{displayPath} has no inputHash");
        var catalogVersion = obj["catalogVersion"] is JsonValue c && c.GetValueKind() == JsonValueKind.Number ? c.GetValue<int>() : 0;
        if (obj["entries"] is not JsonArray entries)
        {
            throw new UsageException($"{displayPath} has no entries array");
        }

        var parsed = new List<LockfileEntry>();
        foreach (var node in entries)
        {
            var id = node?["id"] is JsonValue idValue && idValue.GetValueKind() == JsonValueKind.String ? idValue.GetValue<string>() : null;
            if (id is null)
            {
                throw new UsageException($"{displayPath} has an entry without an id");
            }

            if (!catalog.TryGet(id, out var definition))
            {
                continue;
            }

            parsed.Add(ReadEntry((JsonObject)node!, definition, displayPath));
        }

        return new Lockfile(catalogVersion, inputHash, parsed);
    }

    private static JsonObject ToJson(LockfileEntry entry, DecisionCatalog catalog)
    {
        catalog.TryGet(entry.Id, out var definition);
        var json = new JsonObject();
        RawResultJson.Write(entry.Raw, definition, json);
        json["id"] = entry.Id;
        json["questionHash"] = entry.QuestionHash;
        json["source"] = entry.Source.ToWireName();
        json["value"] = definition is null ? JsonValue.Create(entry.Value) : DecisionValues.ToJson(definition, entry.Value);
        json["resolvedAt"] = entry.ResolvedAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
        if (entry.Model is { } model)
        {
            json["model"] = model;
        }

        if (entry.Thresholds is { } thresholds)
        {
            json["thresholds"] = thresholds.ToJson();
        }

        return json;
    }

    private static LockfileEntry ReadEntry(JsonObject json, DecisionDefinition definition, string displayPath)
    {
        string Fail(string problem) => throw new UsageException($"{displayPath} entry '{definition.Id}' {problem}");

        var questionHash = json["questionHash"] is JsonValue q && q.GetValueKind() == JsonValueKind.String
            ? q.GetValue<string>()
            : Fail("has no questionHash");
        var source = json["source"] is JsonValue s && s.GetValueKind() == JsonValueKind.String
            && DecisionSourceExtensions.TryParse(s.GetValue<string>(), out var parsedSource)
            && parsedSource is DecisionSource.Api or DecisionSource.Fallback or DecisionSource.User or DecisionSource.Script
                ? parsedSource
                : throw new UsageException($"{displayPath} entry '{definition.Id}' has an unsupported source");
        var value = DecisionValues.FromJson(definition, json["value"]) ?? Fail($"has a value that is not a {(definition.IsBoolean ? "boolean" : "string")}");
        var resolvedAt = json["resolvedAt"] is JsonValue r && r.GetValueKind() == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
                ? time
                : throw new UsageException($"{displayPath} entry '{definition.Id}' has no valid resolvedAt");
        var model = json["model"] is JsonValue m && m.GetValueKind() == JsonValueKind.String ? m.GetValue<string>() : null;

        DecisionAnswer raw;
        try
        {
            raw = RawResultJson.Read(json, definition, DecisionSource.Lockfile) with { Model = model };
        }
        catch (FormatException ex)
        {
            throw new UsageException($"{displayPath} entry '{definition.Id}' {ex.Message}", ex);
        }

        return new LockfileEntry(definition.Id, questionHash, source, value, raw, model, resolvedAt, Thresholds.FromJson(json["thresholds"]));
    }
}

/// <summary>The lockfile on disk. A file that fails to read is never overwritten.</summary>
public sealed class LockfileStore(string fullPath, string displayPath, DecisionCatalog catalog)
{
    private bool _readFailed;

    public string DisplayPath { get; } = displayPath;

    /// <summary>The recorded lockfile, or <c>null</c> when there is none.</summary>
    /// <exception cref="UsageException">The file exists but is not a supported lockfile.</exception>
    public Lockfile? Read()
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return LockfileSerializer.Deserialize(File.ReadAllText(fullPath), DisplayPath, catalog);
        }
        catch
        {
            _readFailed = true;
            throw;
        }
    }

    public void Write(Lockfile lockfile)
    {
        if (_readFailed)
        {
            throw new InvalidOperationException($"{DisplayPath} could not be read and must not be overwritten");
        }

        AtomicFile.WriteAllText(fullPath, LockfileSerializer.Serialize(lockfile, catalog));
    }
}
