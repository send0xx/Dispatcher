using Dispatcher.SourceGeneration.Models;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Emission;

internal static class DispatcherEmitter
{
    internal static void Emit(SourceProductionContext context, GenerationResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }

        if (result.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        if (result.MethodName is not null || !result.LocalOpenNotificationHandlers.IsDefaultOrEmpty)
        {
            HandlerRegistrationEmitter.Emit(context, result);
        }

        if (result.DispatcherMethodName is null)
        {
            return;
        }

        GeneratedDispatcherEmitter.Emit(context, result);
        if (!result.OpenBehaviors.IsDefaultOrEmpty)
        {
            PipelineBehaviorEmitter.Emit(context, result);
        }
    }
}