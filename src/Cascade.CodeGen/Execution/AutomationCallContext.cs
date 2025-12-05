using Cascade.Core.Session;
using Cascade.UIAutomation.Session;

namespace Cascade.CodeGen.Execution;

public sealed record AutomationCallContext(
    SessionHandle Session,
    VirtualInputProfile InputProfile,
    Guid RunId,
    CancellationToken Cancellation);

