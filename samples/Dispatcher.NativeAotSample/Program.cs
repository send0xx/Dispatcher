using System.Text.Json.Serialization;
using Dispatcher;
using Dispatcher.Extensions.DependencyInjection;
using Dispatcher.NativeAotSample;
using Dispatcher.SampleApi.Modules.Orders;
using Dispatcher.SampleApi.Modules.Stock;
using FluentValidation;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddDispatcher()
    .AddOrdersModuleAot()
    .AddStockModuleAot()
    .AddPipelineBehavior<
        CreateOrderCommand,
        Guid,
        ValidationCommandBehavior<CreateOrderCommand, Guid>>()
    .AddPipelineBehavior<
        SetStockCommand,
        Unit,
        ValidationCommandBehavior<SetStockCommand, Unit>>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapPost("/orders", async (
    CreateOrderRequest request,
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    var id = await commands.ExecuteAsync(
        new CreateOrderCommand(request.ProductId, request.Quantity),
        cancellationToken);
    return Results.Created($"/orders/{id}", new CreateOrderResponse(id));
});

app.MapGet("/orders", async (IQueryDispatcher queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new ListOrdersQuery(), cancellationToken)));

app.MapGet("/stock/{productId}", async (
    string productId,
    IQueryDispatcher queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new GetStockQuery(productId), cancellationToken)));

app.MapPut("/stock/{productId}", async (
    string productId,
    SetStockRequest request,
    ICommandDispatcher commands,
    CancellationToken cancellationToken) =>
{
    await commands.ExecuteAsync(new SetStockCommand(productId, request.Quantity), cancellationToken);
    return Results.NoContent();
});

app.Run();

public sealed record CreateOrderRequest(string ProductId, int Quantity);
public sealed record CreateOrderResponse(Guid Id);
public sealed record SetStockRequest(int Quantity);

[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(CreateOrderResponse))]
[JsonSerializable(typeof(SetStockRequest))]
[JsonSerializable(typeof(IReadOnlyCollection<Order>))]
[JsonSerializable(typeof(StockLevel))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

public partial class Program;