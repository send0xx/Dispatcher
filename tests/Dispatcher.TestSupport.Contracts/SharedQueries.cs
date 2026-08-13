namespace Dispatcher.TestSupport.Contracts;

public abstract record SharedBaseQuery(string Value) : IQuery<string>;

public sealed record SharedDerivedQuery(string Value) : SharedBaseQuery(Value);

public abstract record LaterBaseQuery(string Value) : IQuery<string>;

public sealed record LaterDerivedQuery(string Value) : LaterBaseQuery(Value);