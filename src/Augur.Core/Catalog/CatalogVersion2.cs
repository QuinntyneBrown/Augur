namespace Augur.Core.Catalog;

/// <summary>
/// The built-in decision catalog, version 2. The instructions and descriptions below are what the model reads,
/// so they decide answer accuracy: change them deliberately, and bump <see cref="Version"/> when you do (L2-009).
/// </summary>
public static class CatalogVersion2
{
    public const int Version = 2;

    private static readonly WhenClause DotNetTargets = new("target", ["fullstack", "dotnet"]);
    private static readonly WhenClause AngularTargets = new("target", ["fullstack", "angular"]);

    public static IReadOnlyList<DecisionDefinition> Build() =>
    [
        new ChoiceDefinition(
            "target",
            "Decide what kind of software the specification asks to be built. Consider the whole specification, including any images of screens.",
            "fullstack",
            When: null,
            [
                new("fullstack", "A web application with both a user interface in the browser and a backend web API that serves its data."),
                new("dotnet", "A backend only: a web API, service, or worker with no browser user interface of its own."),
                new("angular", "A browser user interface only, either with no backend or calling an existing API that is not part of this specification."),
                new("other", "Something that is not a web API or a browser application, such as a mobile app, desktop app, game, library, or script."),
            ]),
        new PredicateDefinition(
            "authentication",
            "Decide whether the software must know who its users are: users sign in, see their own data, or some operations are restricted to signed-in or authorized users.",
            "false",
            When: null),
        new ChoiceDefinition(
            "architecture",
            "Decide how the .NET backend code should be organized, based on how much behaviour and how many features the specification describes.",
            "clean-architecture",
            DotNetTargets,
            [
                new("clean-architecture", "Many features sharing a rich domain model with business rules, where separating domain, application, and infrastructure layers pays off."),
                new("vertical-slice", "Many mostly independent features, each a request with its own handling, where organizing code by feature keeps changes local."),
                new("minimal-api", "A small API with a handful of simple endpoints and little logic beyond storing and returning data."),
                new("other", "The backend needs an organization not described by the other options, such as event sourcing or a microservice mesh."),
            ]),
        new ChoiceDefinition(
            "persistence",
            "Decide how the .NET backend should store its data.",
            "ef-core-sqlite",
            DotNetTargets,
            [
                new("ef-core-sqlserver", "A relational database where the specification names SQL Server or Azure SQL, or an existing Microsoft data platform."),
                new("ef-core-postgresql", "A relational database where the specification names PostgreSQL, or a hosted service built on it."),
                new("ef-core-sqlite", "A relational database where no server product is named, or where the data is small or local to one machine."),
                new("none", "The backend stores no data of its own: it computes results, forwards requests, or only calls other services."),
                new("other", "Storage that is not a relational database, such as a document store, key-value store, or files."),
            ]),
        new PredicateDefinition(
            "background-processing",
            "Decide whether the backend must do work outside of handling a request: scheduled jobs, queues, periodic clean-up, or long-running tasks that finish after the response is sent.",
            "false",
            DotNetTargets),
        new ScoreDefinition(
            "domain-complexity",
            "Rate how much business logic the specification describes, beyond creating, reading, updating, and deleting records.",
            "false",
            new WhenClause("architecture", ["clean-architecture", "vertical-slice"]),
            [
                new("trivial", "Almost no logic: data is stored and returned as it was given."),
                new("crud", "Create, read, update, and delete of records, with simple validation of individual fields."),
                new("business-rules", "Rules that span several records or steps, such as approvals, limits, calculations, or state changes with conditions."),
                new("complex-domain", "Many interacting rules and workflows, where the rules themselves are the main thing being built."),
            ],
            CutOff: 2.0,
            ResolvedId: "cqrs"),
        new ChoiceDefinition(
            "ui-library",
            "Decide which component library the Angular user interface should use.",
            "angular-material",
            AngularTargets,
            [
                new("angular-material", "A conventional application interface with forms, tables, dialogs, and navigation, where standard Material Design components fit."),
                new("none", "A custom or brand-specific visual design, or a very small interface, where plain HTML and CSS fit better than a component library."),
                new("other", "The specification requires a different component library, such as PrimeNG, Bootstrap, or Tailwind-based components."),
            ]),
        new ChoiceDefinition(
            "state-management",
            "Decide how the Angular user interface should manage application state.",
            "signals",
            AngularTargets,
            [
                new("signals", "State that is local to a few screens or simple to share, managed with Angular signals in services."),
                new("ngrx-signal-store", "Shared state used across many screens with coordinated updates, where a structured store with defined methods helps."),
                new("other", "The specification requires a different approach, such as the classic NgRx store with actions and reducers, or another library."),
            ]),
        new PredicateDefinition(
            "server-side-rendering",
            "Decide whether pages must be rendered on the server: public pages that need search-engine indexing, link previews, or fast first display on slow devices.",
            "false",
            AngularTargets),
    ];
}
