namespace Dispatcher;

public sealed record PipelineBehaviorRegistration(Type ServiceType, bool IsReusable);