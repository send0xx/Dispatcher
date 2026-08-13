namespace Dispatcher.NativeAotHostSample.Contracts;

public sealed record Message(Guid Id, string Text);

public abstract record MessagesQuery : IQuery<MessageSnapshot>;

public sealed record ListMessagesQuery : MessagesQuery;

public sealed record AddMessageCommand(string Text) : ICommand<Guid>;

public sealed record ClearMessagesCommand : ICommand;

public abstract record MessageEvent(Guid Id) : INotification;

public sealed record MessageAdded(Guid Id) : MessageEvent(Id);

public sealed record MessageSnapshot(IReadOnlyCollection<Message> Messages, int NotificationsObserved);