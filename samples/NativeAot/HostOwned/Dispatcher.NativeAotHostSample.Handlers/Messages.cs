using System.Collections.Concurrent;
using Dispatcher;

[assembly: GenerateDispatcherHandlers("AddGeneratedMessageHandlers")]

namespace Dispatcher.NativeAotHostSample.Handlers;

public sealed record Message(Guid Id, string Text);
public sealed record ListMessagesQuery : IQuery<MessageSnapshot>;
public sealed record AddMessageCommand(string Text) : ICommand<Guid>;
public sealed record ClearMessagesCommand : ICommand;
public sealed record MessageAdded(Guid Id) : INotification;
public sealed record MessageSnapshot(IReadOnlyCollection<Message> Messages, int NotificationsObserved);

public sealed class MessageStore
{
    private readonly ConcurrentDictionary<Guid, Message> _messages = new();
    private int _notificationsObserved;

    public void Add(Message message) => _messages[message.Id] = message;

    public void Clear() => _messages.Clear();

    public void RecordNotification() => Interlocked.Increment(ref _notificationsObserved);

    public MessageSnapshot Snapshot() => new(
        _messages.Values.OrderBy(message => message.Id).ToArray(),
        Volatile.Read(ref _notificationsObserved));
}

internal sealed class ListMessagesQueryHandler(MessageStore store)
    : IQueryHandler<ListMessagesQuery, MessageSnapshot>
{
    public ValueTask<MessageSnapshot> HandleAsync(
        ListMessagesQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Snapshot());
}

internal sealed class AddMessageCommandHandler(
    MessageStore store,
    INotificationDispatcher dispatcher) : ICommandHandler<AddMessageCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(
        AddMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = new Message(Guid.NewGuid(), command.Text);
        store.Add(message);
        await dispatcher.PublishAsync(new MessageAdded(message.Id), cancellationToken);
        return message.Id;
    }
}

internal sealed class ClearMessagesCommandHandler(MessageStore store)
    : ICommandHandler<ClearMessagesCommand>
{
    public ValueTask HandleAsync(
        ClearMessagesCommand command,
        CancellationToken cancellationToken)
    {
        store.Clear();
        return ValueTask.CompletedTask;
    }
}

internal sealed class CountMessageAddedHandler(MessageStore store)
    : INotificationHandler<MessageAdded>
{
    public ValueTask HandleAsync(
        MessageAdded notification,
        CancellationToken cancellationToken)
    {
        store.RecordNotification();
        return ValueTask.CompletedTask;
    }
}