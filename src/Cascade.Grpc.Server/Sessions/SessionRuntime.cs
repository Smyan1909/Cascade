using Cascade.Core.Session;
using Cascade.UIAutomation.Session;
using System.Windows.Automation;

namespace Cascade.Grpc.Server.Sessions;

public sealed record SessionRuntime(
    SessionHandle Handle,
    VirtualInputChannel InputChannel,
    AutomationElement RootElement);

