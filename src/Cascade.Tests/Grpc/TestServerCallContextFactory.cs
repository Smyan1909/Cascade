using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;

namespace Cascade.Tests.Grpc;

internal static class TestServerCallContextFactory
{
    public static ServerCallContext Create(string method, Metadata? headers = null)
    {
        return TestServerCallContext.Create(
            method,
            null,
            DateTime.UtcNow.AddMinutes(1),
            headers ?? new Metadata(),
            CancellationToken.None,
            "127.0.0.1",
            null,
            null,
            _ => Task.CompletedTask,
            () => new WriteOptions(),
            _ => { });
    }
}

