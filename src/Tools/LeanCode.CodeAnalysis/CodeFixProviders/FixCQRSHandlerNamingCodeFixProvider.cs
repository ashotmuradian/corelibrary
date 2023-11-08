using System.Collections.Immutable;
using System.Composition;
using LeanCode.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace LeanCode.CodeAnalysis.CodeFixProviders;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixCancellationTokenNamingCodeFixProvider))]
[Shared]
public class FixCQRSHandlerNamingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            DiagnosticsIds.CommandHandlersShouldFollowNamingConvention,
            DiagnosticsIds.QueryHandlersShouldFollowNamingConvention,
            DiagnosticsIds.OperationHandlersShouldFollowNamingConvention
        );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(new FixCQRSHandlerNamingAction(context.Document, context.Span), context.Diagnostics);

        return Task.CompletedTask;
    }
}
