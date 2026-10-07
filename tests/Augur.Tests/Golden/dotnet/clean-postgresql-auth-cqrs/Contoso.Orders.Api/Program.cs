using Contoso.Orders.Api;
using Contoso.Orders.Api.Authentication;
using Contoso.Orders.Api.Notes;
using Contoso.Orders.Application;
using Contoso.Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (!AuthenticationStartup.TryConfigure(builder))
{
    return 1;
}

CorsStartup.Configure(builder);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

CorsStartup.Use(app);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoint();
app.MapNotesEndpoints();

app.Run();
return 0;

/// <summary>The API's entry point, public so tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
