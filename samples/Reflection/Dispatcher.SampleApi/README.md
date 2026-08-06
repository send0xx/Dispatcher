# Dispatcher Minimal API sample

This .NET 10 sample keeps all state in memory so the CQRS flow is easy to follow.

Start it from the repository root:

```bash
dotnet run --project samples/Reflection/Dispatcher.SampleApi
```

Set stock, create an order, and observe that the notification handler reserves stock:

```bash
curl -X PUT http://localhost:5000/stock/keyboard \
  -H "Content-Type: application/json" \
  -d '{"quantity":10}'

curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":"keyboard","quantity":3}'

curl http://localhost:5000/stock/keyboard
```

The final response reports a quantity of `7`.

An invalid command demonstrates the FluentValidation behavior:

```bash
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"productId":"","quantity":0}'
```

The request flow is:

```text
Minimal API endpoint
  -> ICommandDispatcher.ExecuteAsync
  -> FluentValidation command behavior
  -> internal CreateOrderCommandHandler
  -> INotificationDispatcher.PublishAsync
  -> internal Stock notification handler
```

The web project registers Dispatcher infrastructure once. Orders and Stock register their internal handlers independently through their public module registration methods.
