namespace Dispatcher.TestSupport.Contracts;

public abstract record SharedBaseQuery(string Value) : IQuery<string>;

public sealed record SharedDerivedQuery(string Value) : SharedBaseQuery(Value);

public abstract record LaterBaseQuery(string Value) : IQuery<string>;

public sealed record LaterDerivedQuery(string Value) : LaterBaseQuery(Value);

public sealed record OpenOnlyNotification : INotification;

public abstract record SharedNotification : INotification;

public sealed record DerivedSharedNotification : SharedNotification;

public sealed record ExactSharedNotification : SharedNotification;

public interface IRestrictedNotification : INotification;