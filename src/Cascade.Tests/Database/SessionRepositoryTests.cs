using Cascade.Database.Entities;
using Cascade.Database.Enums;
using Cascade.Database.Repositories.Implementations;
using FluentAssertions;
using Xunit;

namespace Cascade.Tests.Database;

public class SessionRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistSession()
    {
        using var context = _factory.CreateContext();
        var repository = new SessionRepository(context);
        var agent = await CreateAgentAsync(context);

        var session = new AutomationSession
        {
            AgentId = agent.Id,
            SessionId = "session-123",
            RunId = "run-123"
        };

        var saved = await repository.CreateAsync(session);

        saved.Id.Should().NotBe(Guid.Empty);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        saved.State.Should().Be(SessionState.Active);
    }

    [Fact]
    public async Task GetBySessionIdAsync_ShouldReturnSession_WithEventsOptional()
    {
        Guid agentId;
        using (var seedContext = _factory.CreateContext())
        {
            agentId = (await CreateAgentAsync(seedContext)).Id;
        }

        using (var context = _factory.CreateContext())
        {
            var repository = new SessionRepository(context);
            var session = await repository.CreateAsync(new AutomationSession
            {
                AgentId = agentId,
                SessionId = "session-get",
                RunId = "run-get"
            });

            await repository.AddEventAsync(new SessionEvent
            {
                AutomationSessionId = session.Id,
                EventType = "Heartbeat",
                Payload = "{}"
            });
        }

        using (var context = _factory.CreateContext())
        {
            var repository = new SessionRepository(context);
            var noEvents = await repository.GetBySessionIdAsync("session-get");
            noEvents.Should().NotBeNull();
            noEvents!.Events.Should().BeEmpty();
        }

        using (var context = _factory.CreateContext())
        {
            var repository = new SessionRepository(context);
            var withEvents = await repository.GetBySessionIdAsync("session-get", includeEvents: true);
            withEvents.Should().NotBeNull();
            withEvents!.Events.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task UpdateStateAsync_ShouldSetReleasedTimestamp()
    {
        using var context = _factory.CreateContext();
        var repository = new SessionRepository(context);
        var agent = await CreateAgentAsync(context);

        await repository.CreateAsync(new AutomationSession
        {
            AgentId = agent.Id,
            SessionId = "session-release",
            RunId = "run-release"
        });

        await repository.UpdateStateAsync("session-release", SessionState.Draining);
        var draining = await repository.GetBySessionIdAsync("session-release");
        draining!.State.Should().Be(SessionState.Draining);
        draining.ReleasedAt.Should().BeNull();

        await repository.UpdateStateAsync("session-release", SessionState.Released);
        var released = await repository.GetBySessionIdAsync("session-release");
        released!.State.Should().Be(SessionState.Released);
        released.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_ShouldAddReleaseEvent()
    {
        using var context = _factory.CreateContext();
        var repository = new SessionRepository(context);
        var agent = await CreateAgentAsync(context);

        await repository.CreateAsync(new AutomationSession
        {
            AgentId = agent.Id,
            SessionId = "session-finalize",
            RunId = "run-finalize"
        });

        await repository.ReleaseAsync("session-finalize", "completed");

        var session = await repository.GetBySessionIdAsync("session-finalize", includeEvents: true);
        session!.State.Should().Be(SessionState.Released);
        session.ReleasedAt.Should().NotBeNull();
        session.Events.Should().ContainSingle(e => e.EventType == "Released");
    }

    private static async Task<Agent> CreateAgentAsync(Cascade.Database.Context.CascadeDbContext context)
    {
        var agent = new Agent
        {
            Name = $"Agent-{Guid.NewGuid()}",
            TargetApplication = "TestApp"
        };

        context.Agents.Add(agent);
        await context.SaveChangesAsync();
        return agent;
    }
}



