namespace Dispatcher.SourceGeneration.Model;

internal enum HandlerKind
{
    Query,
    CommandWithResponse,
    Command,
    Notification
}