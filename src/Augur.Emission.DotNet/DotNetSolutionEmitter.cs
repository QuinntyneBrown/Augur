namespace Augur.Emission.DotNet;

/// <summary>Emits the .NET solution for <c>dotnet</c> and <c>fullstack</c> plans.</summary>
public sealed class DotNetSolutionEmitter : IEmitter
{
    private readonly TemplateEngine _templates = new(typeof(DotNetSolutionEmitter).Assembly);

    public FileSet Emit(EmissionContext context)
    {
        var files = new FileSet();
        if (context.Target is not ("dotnet" or "fullstack"))
        {
            return files;
        }

        var model = new DotNetModel(
            context.SolutionName.Value,
            context.Value("architecture")!,
            context.Value("persistence")!,
            context.IsTrue("authentication"),
            context.IsTrue("background-processing"),
            context.IsTrue("domain-complexity"),
            context.Target == "fullstack",
            context.AngularProjectName,
            context.IsTrue("server-side-rendering"));

        files.Add($"{model.Name}.slnx", Render("solution/slnx.sbn", model));
        files.Add("Directory.Build.props", Render("solution/Directory.Build.props.sbn", model));
        files.AddBinary(".editorconfig", _templates.Static("solution/editorconfig"));
        files.AddBinary(".gitignore", _templates.Static("solution/gitignore"));

        AddApi(files, model);
        if (model.Clean)
        {
            AddCleanArchitectureLayers(files, model);
        }

        if (model.Worker)
        {
            var worker = model.WorkerProject;
            files.Add($"{worker}/{worker}.csproj", Render("worker/Worker.csproj.sbn", model));
            files.Add($"{worker}/Program.cs", Render("worker/Program.cs.sbn", model));
            files.Add($"{worker}/ScheduledWorker.cs", Render("worker/ScheduledWorker.cs.sbn", model));
            files.Add($"{worker}/appsettings.json", Render("worker/appsettings.json.sbn", model));
        }

        var tests = model.TestsProject;
        files.Add($"{tests}/{tests}.csproj", Render("tests/Tests.csproj.sbn", model));
        files.Add($"{tests}/ApiTests.cs", Render("tests/ApiTests.cs.sbn", model));
        if (model.Worker)
        {
            files.Add($"{tests}/WorkerTests.cs", Render("tests/WorkerTests.cs.sbn", model));
        }

        return files;
    }

    private void AddApi(FileSet files, DotNetModel model)
    {
        var api = model.ApiProject;
        files.Add($"{api}/{api}.csproj", Render("api/Api.csproj.sbn", model));
        files.Add($"{api}/Program.cs", Render("api/Program.cs.sbn", model));
        files.Add($"{api}/HealthEndpoint.cs", Render("api/HealthEndpoint.cs.sbn", model));
        files.Add($"{api}/appsettings.json", Render("api/appsettings.json.sbn", model));
        files.Add($"{api}/appsettings.Development.json", Render("api/appsettings.Development.json.sbn", model));
        files.Add($"{api}/Properties/launchSettings.json", Render("api/launchSettings.json.sbn", model));
        if (model.Cors)
        {
            files.Add($"{api}/CorsStartup.cs", Render("api/CorsStartup.cs.sbn", model));
        }

        if (model.Auth)
        {
            files.Add($"{api}/Authentication/AuthenticationStartup.cs", Render("api/AuthenticationStartup.cs.sbn", model));
        }

        if (model.Fullstack)
        {
            files.Add($"{api}/SpaStartup.cs", Render("api/SpaStartup.cs.sbn", model));
        }

        if (model.Clean)
        {
            files.Add($"{api}/Notes/NotesEndpoints.cs", Render("clean/api/NotesEndpoints.cs.sbn", model));
            return;
        }

        var notes = model.VerticalSlice ? $"{api}/Features/Notes" : $"{api}/Notes";
        files.Add($"{notes}/Note.cs", Render("notes/Note.cs.sbn", model));
        files.Add($"{notes}/INoteStore.cs", Render("notes/INoteStore.cs.sbn", model));
        files.Add($"{notes}/{(model.Ef ? "EfNoteStore" : "InMemoryNoteStore")}.cs", Render(model.Ef ? "notes/EfNoteStore.cs.sbn" : "notes/InMemoryNoteStore.cs.sbn", model));
        files.Add($"{notes}/NotesServices.cs", Render("notes/NotesServices.cs.sbn", model));
        if (model.Ef)
        {
            files.Add($"{api}/Data/NotesDbContext.cs", Render("notes/NotesDbContext.cs.sbn", model));
        }

        if (model.MinimalApi)
        {
            files.Add($"{notes}/NotesEndpoints.cs", Render("notes/NotesEndpoints.cs.sbn", model));
        }
        else if (model.Cqrs)
        {
            files.Add($"{notes}/CreateNote.cs", Render("vertical/CreateNoteCqrs.cs.sbn", model));
            files.Add($"{notes}/ListNotes.cs", Render("vertical/ListNotesCqrs.cs.sbn", model));
            files.Add($"{notes}/NotesFeature.cs", Render("vertical/NotesFeature.cs.sbn", model));
        }
        else
        {
            files.Add($"{notes}/CreateNote.cs", Render("vertical/CreateNote.cs.sbn", model));
            files.Add($"{notes}/ListNotes.cs", Render("vertical/ListNotes.cs.sbn", model));
            files.Add($"{notes}/NotesFeature.cs", Render("vertical/NotesFeature.cs.sbn", model));
        }
    }

    private void AddCleanArchitectureLayers(FileSet files, DotNetModel model)
    {
        var domain = $"{model.Name}.Domain";
        files.Add($"{domain}/{domain}.csproj", Render("clean/domain/Domain.csproj.sbn", model));
        files.Add($"{domain}/Notes/Note.cs", Render("clean/domain/Note.cs.sbn", model));

        var application = $"{model.Name}.Application";
        files.Add($"{application}/{application}.csproj", Render("clean/application/Application.csproj.sbn", model));
        files.Add($"{application}/Notes/INoteRepository.cs", Render("clean/application/INoteRepository.cs.sbn", model));
        files.Add($"{application}/Notes/NoteValidation.cs", Render("clean/application/NoteValidation.cs.sbn", model));
        files.Add($"{application}/DependencyInjection.cs", Render("clean/application/DependencyInjection.cs.sbn", model));
        if (model.Cqrs)
        {
            files.Add($"{application}/Notes/Commands/CreateNote.cs", Render("clean/application/CreateNoteCommand.cs.sbn", model));
            files.Add($"{application}/Notes/Queries/ListNotes.cs", Render("clean/application/ListNotesQuery.cs.sbn", model));
        }
        else
        {
            files.Add($"{application}/Notes/NoteService.cs", Render("clean/application/NoteService.cs.sbn", model));
        }

        var infrastructure = $"{model.Name}.Infrastructure";
        files.Add($"{infrastructure}/{infrastructure}.csproj", Render("clean/infrastructure/Infrastructure.csproj.sbn", model));
        files.Add($"{infrastructure}/DependencyInjection.cs", Render("clean/infrastructure/DependencyInjection.cs.sbn", model));
        if (model.Ef)
        {
            files.Add($"{infrastructure}/Persistence/NotesDbContext.cs", Render("notes/NotesDbContext.cs.sbn", model));
            files.Add($"{infrastructure}/Persistence/EfNoteRepository.cs", Render("clean/infrastructure/EfNoteRepository.cs.sbn", model));
        }
        else
        {
            files.Add($"{infrastructure}/Notes/InMemoryNoteRepository.cs", Render("clean/infrastructure/InMemoryNoteRepository.cs.sbn", model));
        }
    }

    private string Render(string template, DotNetModel model) => _templates.Render(template, model);
}
