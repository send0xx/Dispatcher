namespace Dispatcher;

public enum HandlerKind
{
    Query,
    Command,
    CommandWithResponse,
    Notification
}