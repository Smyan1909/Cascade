using Cascade.Body.Automation;
using Cascade.Proto;
using Grpc.Core;
using ActionProto = Cascade.Proto.Action;
using StatusProto = Cascade.Proto.Status;

namespace Cascade.Body.Services;

public class AutomationService : Proto.AutomationService.AutomationServiceBase
{
    private readonly AutomationRouter _router;

    public AutomationService(AutomationRouter router)
    {
        _router = router;
    }

    public override async Task<StatusProto> PerformAction(ActionProto request, ServerCallContext context)
    {
        var provider = _router.GetProvider(request.Selector?.PlatformSource);
        if (provider is null)
        {
            return new StatusProto { Success = false, Message = "No provider available for requested platform" };
        }

        return await provider.PerformActionAsync(request, context.CancellationToken).ConfigureAwait(false);
    }

    public override async Task<SemanticTree> GetSemanticTree(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
    {
        var provider = _router.GetProvider(PlatformSource.Unspecified);
        if (provider is null)
        {
            return new SemanticTree();
        }

        return await provider.GetSemanticTreeAsync(context.CancellationToken).ConfigureAwait(false);
    }
}

