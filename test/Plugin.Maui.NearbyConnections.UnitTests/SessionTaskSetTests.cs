using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers <see cref="SessionTaskSet"/> — the owner of live session tasks (contract C6). Stop and
/// Dispose join it: the join loops over tasks added mid-join and gives up at its bound.
/// </summary>
[Trait("Category", "Session")]
public class SessionTaskSetTests
{
    static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task QuietSet_JoinsImmediately()
    {
        // Arrange
        var set = Create.TaskSet(new FakeTimeProvider());
        set.Add(Task.CompletedTask);

        // Act
        var quiet = await set.JoinAsync(Bound);

        // Assert
        Assert.True(quiet);
    }

    [Fact]
    public async Task JoinAsync_WaitsForALiveTask()
    {
        // Arrange
        var gate = Create.Gate();
        var set = Create.TaskSet(new FakeTimeProvider());
        set.Add(gate.Task);
        var join = set.JoinAsync(Bound);

        // Act
        gate.SetResult();
        var quiet = await join;

        // Assert
        Assert.True(quiet);
    }

    [Fact]
    public async Task JoinAsync_GivesUpAtTheBound()
    {
        // Arrange — a task nothing will complete.
        var time = new FakeTimeProvider();
        var set = Create.TaskSet(time);
        set.Add(Create.Gate().Task);
        var join = set.JoinAsync(Bound);

        // Act
        time.Advance(Bound + TimeSpan.FromSeconds(1));
        var quiet = await join;

        // Assert
        Assert.False(quiet);
    }

    [Fact]
    public async Task TaskAddedDuringAJoin_IsJoinedToo()
    {
        // Arrange
        var first = Create.Gate();
        var second = Create.Gate();
        var set = Create.TaskSet(new FakeTimeProvider());
        set.Add(first.Task);
        var join = set.JoinAsync(Bound);
        set.Add(second.Task);

        // Act — completing only the first must not end the join.
        first.SetResult();
        await Task.Yield();
        Assert.False(join.IsCompleted);
        second.SetResult();
        var quiet = await join;

        // Assert
        Assert.True(quiet);
    }

    [Fact]
    public async Task FaultingMember_IsReportedAndTheSetGoesQuiet()
    {
        // Arrange
        var failures = new List<Exception>();
        var gate = Create.Gate();
        var set = Create.TaskSet(new FakeTimeProvider(), failures.Add);
        set.Add(FaultAfterAsync(gate.Task));
        var join = set.JoinAsync(Bound);

        // Act
        gate.SetResult();
        var quiet = await join;
        await Wait.UntilAsync(() => failures.Count == 1);

        // Assert
        Assert.True(quiet);
        Assert.Equal("member broke", Assert.Single(failures).Message);

        static async Task FaultAfterAsync(Task released)
        {
            await released;
            throw new InvalidOperationException("member broke");
        }
    }
}
