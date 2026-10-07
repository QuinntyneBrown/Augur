using Contoso.Orders.Api;
using Contoso.Orders.Api.Notes;

var builder = WebApplication.CreateBuilder(args);
CorsStartup.Configure(builder);
builder.Services.AddNotes(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

CorsStartup.Use(app);

app.MapHealthEndpoint();
app.MapNotesEndpoints();

app.Run();
return 0;

/// <summary>The API's entry point, public so tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
