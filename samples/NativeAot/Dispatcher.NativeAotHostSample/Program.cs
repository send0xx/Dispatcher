using System.Text.Json.Serialization;
using Dispatcher;
using Dispatcher.NativeAotHostSample;
using Dispatcher.NativeAotHostSample.Audit;
using Dispatcher.NativeAotHostSample.Contracts;
using Dispatcher.NativeAotHostSample.Handlers;
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcher("AddDispatcher")]

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddDispatcher()
    .AddMessageHandlers()
    .AddAuditHandlers()
    .AddPipelineBehavior(typeof(LoggingBehavior<,>))
    .AddSingleton<MessageStore>()
    .AddSingleton<AuditState>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = builder.Build();

app.MapPost("/messages", async (
    AddMessageRequest request,
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    var id = await commands.ExecuteAsync(new AddMessageCommand(request.Text), cancellationToken);
    return Results.Ok(new AddMessageResponse(id));
});

app.MapDelete("/messages", async (
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    await commands.ExecuteAsync(new ClearMessagesCommand(), cancellationToken);
    return Results.NoContent();
});

app.MapGet("/messages", async (IQueryDispatcher queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new ListMessagesQuery(), cancellationToken)));

app.MapGet("/audit", async (IQueryDispatcher queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new GetAuditCountQuery(), cancellationToken)));

app.MapPost("/audit/pulse", async (INotificationDispatcher notifications, CancellationToken cancellationToken) =>
{
    await notifications.PublishAsync(new AuditPulse(), cancellationToken);
    return Results.NoContent();
});

await app.RunAsync();

public sealed record AddMessageRequest(string Text);

public sealed record AddMessageResponse(Guid Id);

[JsonSerializable(typeof(AddMessageRequest))]
[JsonSerializable(typeof(AddMessageResponse))]
[JsonSerializable(typeof(MessageSnapshot))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

public partial class Program;