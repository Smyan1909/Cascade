using Cascade.Core;
using Cascade.Database.Entities;
using Cascade.Database.Enums;
using ProtoResult = Cascade.Grpc.Result;
using ProtoProfile = Cascade.Grpc.Session.VirtualDesktopProfile;
using ProtoSessionContext = Cascade.Grpc.SessionContext;
using ProtoSessionResponse = Cascade.Grpc.Session.SessionResponse;
using ProtoSessionState = Cascade.Grpc.Session.SessionState;
using ProtoSessionEvent = Cascade.Grpc.Session.SessionEvent;
using DbSessionState = Cascade.Database.Enums.SessionState;
using Cascade.Grpc.Server.Sessions;
using CoreVirtualDesktopProfile = Cascade.Core.VirtualDesktopProfile;

namespace Cascade.Grpc.Server.Mappers;

internal static class SessionMappingExtensions
{
    public static CoreVirtualDesktopProfile ToDomainProfile(this ProtoProfile? profile)
    {
        var defaults = CoreVirtualDesktopProfile.Default;
        if (profile is null)
        {
            return defaults;
        }

        return new CoreVirtualDesktopProfile
        {
            Width = profile.Width == 0 ? defaults.Width : profile.Width,
            Height = profile.Height == 0 ? defaults.Height : profile.Height,
            Dpi = profile.Dpi == 0 ? defaults.Dpi : profile.Dpi,
            EnableGpu = profile.EnableGpu
        };
    }

    public static ProtoProfile ToProto(this CoreVirtualDesktopProfile profile)
    {
        return new ProtoProfile
        {
            Width = profile.Width,
            Height = profile.Height,
            Dpi = profile.Dpi,
            EnableGpu = profile.EnableGpu
        };
    }

    public static ProtoSessionContext ToProtoContext(this AutomationSession session)
    {
        return new ProtoSessionContext
        {
            SessionId = session.SessionId,
            AgentId = session.AgentId.ToString(),
            RunId = session.RunId
        };
    }

    public static ProtoSessionResponse ToSessionResponse(this AutomationSession session, ProtoResult? result = null)
    {
        return new ProtoSessionResponse
        {
            Result = result ?? new ProtoResult { Success = true },
            Session = session.ToProtoContext(),
            Profile = session.Profile?.ToProto(),
            State = session.State.ToProtoState()
        };
    }

    public static ProtoSessionState ToProtoState(this DbSessionState state)
    {
        return state switch
        {
            DbSessionState.Draining => ProtoSessionState.SessionDraining,
            DbSessionState.Released => ProtoSessionState.SessionTerminated,
            DbSessionState.Failed => ProtoSessionState.SessionTerminated,
            DbSessionState.Active => ProtoSessionState.SessionReady,
            _ => ProtoSessionState.SessionReady
        };
    }

    public static ProtoSessionEvent ToProtoEvent(this SessionEventMessage message)
    {
        return new ProtoSessionEvent
        {
            Session = new ProtoSessionContext
            {
                SessionId = message.SessionId,
                AgentId = message.AgentId ?? string.Empty,
                RunId = message.RunId ?? string.Empty
            },
            State = message.State,
            Message = message.Message
        };
    }
}

