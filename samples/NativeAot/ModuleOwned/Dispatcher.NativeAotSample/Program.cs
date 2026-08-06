using System.Text.Json.Serialization;
using Dispatcher;
using Dispatcher.Extensions.Microsoft.DependencyInjection;
using Dispatcher.NativeAotSample;
using Dispatcher.NativeAotSample.Module;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddDispatcher()
    .AddCounterModule()
    .AddPipelineBehavior<
        IncrementCounterCommand,
        int,
        ValidationCommandBehavior<IncrementCounterCommand, int>>()
    .AddPipelineBehavior<
        ResetCounterCommand,
        Unit,
        ValidationCommandBehavior<ResetCounterCommand, Unit>>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapPost("/counter/increment", async (
    IncrementCounterRequest request,
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    var value = await commands.ExecuteAsync(
        new IncrementCounterCommand(request.Amount),
        cancellationToken);
    return Results.Ok(new IncrementCounterResponse(value));
});

app.MapDelete("/counter", async (
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    await commands.ExecuteAsync(new ResetCounterCommand(), cancellationToken);
    return Results.NoContent();
});

app.MapGet("/counter", async (IQueryDispatcher queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new GetCounterQuery(), cancellationToken)));

app.Run();

public sealed record IncrementCounterRequest(int Amount);
public sealed record IncrementCounterResponse(int Value);

[JsonSerializable(typeof(IncrementCounterRequest))]
[JsonSerializable(typeof(IncrementCounterResponse))]
[JsonSerializable(typeof(CounterSnapshot))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

public partial class Program;