using Dispatcher.SourceGeneration.Model;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Emission;

internal static class SourceOutputEmitter
{
    internal static void Emit(SourceProductionContext output, GenerationModel generation)
    {
        foreach (var diagnostic in generation.Diagnostics)
        {
            output.ReportDiagnostic(diagnostic);
        }

        if (generation.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        if (generation.HandlerRegistrationMethod is not null ||
            !generation.LocalOpenNotificationHandlers.IsDefaultOrEmpty)
        {
            HandlerRegistrationEmitter.Emit(output, generation);
        }

        if (generation.DispatcherRegistrationMethod is null)
        {
            return;
        }

        var dispatcher = new DispatcherSourceModel(generation);
        DispatcherImplementationEmitter.Emit(output, dispatcher);
        DispatcherRegistrationEmitter.Emit(output, generation, dispatcher);
        if (!generation.PipelineBehaviors.IsDefaultOrEmpty)
        {
            PipelineBehaviorRegistrationEmitter.Emit(output, generation);
        }
    }
}