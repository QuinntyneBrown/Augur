namespace Augur.Emission.DotNet;

/// <summary>Exact versions of every package an emitted .NET solution references.</summary>
public static class PackageVersions
{
    public const string AspNetCore = "10.0.12";
    public const string EntityFrameworkCore = "10.0.12";
    public const string Npgsql = "10.0.3";
    public const string Extensions = "10.0.12";
    public const string TestSdk = "18.10.1";
    public const string Xunit = "2.9.3";
    public const string XunitRunner = "3.1.5";
}

/// <summary>Everything the .NET templates may use, derived only from validated plan values and the solution name.</summary>
public sealed record DotNetModel(
    string Name,
    string Architecture,
    string Persistence,
    bool Auth,
    bool Worker,
    bool Cqrs,
    bool Fullstack,
    string AngularProjectName,
    bool Ssr)
{
    /// <summary>The page the published Angular app starts from; server-side rendering renames it.</summary>
    public string IndexFile => Ssr ? "index.csr.html" : "index.html";

    public bool Ef => Persistence != "none";

    public bool Clean => Architecture == "clean-architecture";

    public bool VerticalSlice => Architecture == "vertical-slice";

    public bool MinimalApi => Architecture == "minimal-api";

    /// <summary>CORS is configurable for a standalone API; a fullstack app serves its UI from the same origin.</summary>
    public bool Cors => !Fullstack;

    public string ApiProject => $"{Name}.Api";

    public string TestsProject => $"{Name}.Tests";

    public string WorkerProject => $"{Name}.Worker";

    /// <summary>The namespace that holds the notes feature in the API project.</summary>
    public string NotesNamespace => VerticalSlice ? $"{Name}.Api.Features.Notes" : $"{Name}.Api.Notes";

    /// <summary>The namespace that holds <c>NotesDbContext</c>.</summary>
    public string DataNamespace => Clean ? $"{Name}.Infrastructure.Persistence" : $"{Name}.Api.Data";

    /// <summary>The namespace that holds the <c>Note</c> entity.</summary>
    public string NoteNamespace => Clean ? $"{Name}.Domain.Notes" : NotesNamespace;

    public string ProviderPackage => Persistence switch
    {
        "ef-core-sqlserver" => "Microsoft.EntityFrameworkCore.SqlServer",
        "ef-core-postgresql" => "Npgsql.EntityFrameworkCore.PostgreSQL",
        _ => "Microsoft.EntityFrameworkCore.Sqlite",
    };

    public string ProviderVersion => Persistence == "ef-core-postgresql" ? PackageVersions.Npgsql : PackageVersions.EntityFrameworkCore;

    public string UseProvider => Persistence switch
    {
        "ef-core-sqlserver" => "UseSqlServer",
        "ef-core-postgresql" => "UseNpgsql",
        _ => "UseSqlite",
    };

    /// <summary>A development connection string. None of them contains a password.</summary>
    public string ConnectionString => Persistence switch
    {
        "ef-core-sqlserver" => $"Server=localhost;Database={DatabaseName};Trusted_Connection=True",
        "ef-core-postgresql" => $"Host=localhost;Database={DatabaseName.ToLowerInvariant()};Username=postgres",
        _ => $"Data Source={DatabaseName}.db",
    };

    /// <summary>Tests can exercise the notes endpoints only when they need no database server and no token.</summary>
    public bool TestsNotes => !Auth && Persistence is "none" or "ef-core-sqlite";

    public bool TestsSqlite => TestsNotes && Persistence == "ef-core-sqlite";

    public string DatabaseName => Name.Replace(".", "", StringComparison.Ordinal);

    public string AspNetCoreVersion => PackageVersions.AspNetCore;

    public string ExtensionsVersion => PackageVersions.Extensions;

    public string TestSdkVersion => PackageVersions.TestSdk;

    public string XunitVersion => PackageVersions.Xunit;

    public string XunitRunnerVersion => PackageVersions.XunitRunner;

    public int HttpsPort => 7000 + (StableHash(Name) % 1000);

    public int HttpPort => 5000 + (StableHash(Name) % 1000);

    /// <summary>A hash that is the same on every machine and run, unlike <see cref="string.GetHashCode()"/>.</summary>
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked((hash * 31) + c);
        }

        return Math.Abs(hash % 100_000);
    }
}
