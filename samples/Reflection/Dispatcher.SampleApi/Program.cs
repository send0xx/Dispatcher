using Dispatcher;
using Dispatcher.Extensions.Microsoft.DependencyInjection;
using Dispatcher.SampleApi;
using Dispatcher.SampleApi.Modules.Orders;
using Dispatcher.SampleApi.Modules.Stock;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDispatcher()
    .AddPipelineBehavior(typeof(ValidationCommandBehavior<,>))
    .AddOrdersModule()
    .AddStockModule();
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
    return Results.Created($"/orders/{id}", new { id });
});

app.MapGet("/orders", async (IQueryDispatcher queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.QueryAsync(new ListOrdersQuery(), cancellationToken)));

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    IQueryDispatcher queries,
    CancellationToken cancellationToken) =>
{
    var order = await queries.QueryAsync(new GetOrderQuery(id), cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

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
public sealed record SetStockRequest(int Quantity);

public partial class Program;