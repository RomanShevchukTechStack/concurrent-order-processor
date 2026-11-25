using OrderProcessor.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.AddSingleton<FailureSimulator>();

var app = builder.Build();

// Settings come from config or command line, e.g.:
//   dotnet run -- --Simulation:DelayMs=100 --Simulation:ErrorRate=0.1 --Simulation:Seed=42
app.MapGet("/products/{id}", async (string id, FailureSimulator simulator, CancellationToken ct) =>
{
    if (simulator.DelayMs > 0)
    {
        await Task.Delay(simulator.DelayMs, ct);
    }

    switch (simulator.NextFailure(id))
    {
        case FailureKind.ServerError:
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        case FailureKind.TooManyRequests:
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        case FailureKind.Timeout:
            await Task.Delay(simulator.TimeoutMs, ct);
            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }

    return ProductCatalog.TryGet(id, out var product)
        ? Results.Ok(product)
        : Results.NotFound();
});

app.Run();

// Needed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
