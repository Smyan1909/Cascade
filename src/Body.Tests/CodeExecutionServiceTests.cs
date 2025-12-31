using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cascade.Proto;
using FluentAssertions;
using Grpc.Core;
using Xunit;
using GrpcStatus = Grpc.Core.Status;
using BodyCodeExecutionService = Cascade.Body.Services.CodeExecutionService;

namespace Cascade.Body.Tests;

public class CodeExecutionServiceTests
{
    [Fact]
    public async Task ExecuteCode_ReturnsSuccess_ForValidEntrypoint()
    {
        var service = new BodyCodeExecutionService();
        var req = new CodeExecutionRequest
        {
            Language = "csharp",
            SkillId = "skill-1",
            UserId = "user-1",
            AppId = "app-1",
        };
        req.Files.Add(new CodeFile
        {
            Path = "Skill.cs",
            Language = "csharp",
            Content = @"
using System;
public static class SkillEntrypoint
{
    public static string Run(string inputsJson)
    {
        return ""ok"";
    }
}
"
        });

        var ctx = new FakeServerCallContext(deadline: DateTime.UtcNow.AddSeconds(5));
        var res = await service.ExecuteCode(req, ctx);

        res.Success.Should().BeTrue();
        res.Error.Should().BeNullOrEmpty();
        res.Output.Should().Contain("ok");
    }

    [Fact]
    public async Task ExecuteCode_Denies_SystemIO()
    {
        var service = new BodyCodeExecutionService();
        var req = new CodeExecutionRequest
        {
            Language = "csharp",
            SkillId = "skill-1",
            UserId = "user-1",
            AppId = "app-1",
        };
        req.Files.Add(new CodeFile
        {
            Path = "Skill.cs",
            Language = "csharp",
            Content = @"
using System.IO;
public static class SkillEntrypoint
{
    public static string Run(string inputsJson)
    {
        return ""nope"";
    }
}
"
        });

        var ctx = new FakeServerCallContext(deadline: DateTime.UtcNow.AddSeconds(5));
        var res = await service.ExecuteCode(req, ctx);

        res.Success.Should().BeFalse();
        res.Error.Should().Contain("denied", Exactly.Once());
    }

    [Fact]
    public async Task ExecuteCode_RespectsDeadlineTimeout()
    {
        var service = new BodyCodeExecutionService();
        var req = new CodeExecutionRequest
        {
            Language = "csharp",
            SkillId = "skill-1",
            UserId = "user-1",
            AppId = "app-1",
        };
        req.Files.Add(new CodeFile
        {
            Path = "Skill.cs",
            Language = "csharp",
            Content = @"
public static class SkillEntrypoint
{
    public static string Run(string inputsJson)
    {
        while (true) { }
    }
}
"
        });

        var ctx = new FakeServerCallContext(deadline: DateTime.UtcNow.AddMilliseconds(150));
        var res = await service.ExecuteCode(req, ctx);

        res.Success.Should().BeFalse();
        res.Error.Should().Contain("timed out");
    }

    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly CancellationToken _ct;
        private readonly DateTime _deadline;

        public FakeServerCallContext(DateTime deadline, CancellationToken cancellationToken = default)
        {
            _deadline = deadline;
            _ct = cancellationToken;
        }

        protected override string MethodCore => "CodeExecutionService/ExecuteCode";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => _deadline;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => _ct;
        protected override Metadata ResponseTrailersCore => new();
        protected override GrpcStatus StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new("insecure", new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotImplementedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}


