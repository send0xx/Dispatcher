namespace Dispatcher.Tests.Contracts;

public abstract record SharedBaseQuery(string Value) : IQuery<string>;

public sealed record SharedDerivedQuery(string Value) : SharedBaseQuery(Value);