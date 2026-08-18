using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NearbyChat.ViewModels;

/// <summary>
/// The base for a page view model, scoping every resource it owns to the window between
/// <see cref="NavigatedTo"/> and <see cref="NavigatedFrom"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not <see cref="IDisposable"/>.</b> .NET MAUI never calls <c>Dispose</c> on a
/// page or its binding context — <c>Page</c> does not implement <see cref="IDisposable"/> at all,
/// and Shell drops a popped page by unparenting it and letting the garbage collector take it.
/// Worse, a transient registered by type that implements <see cref="IDisposable"/> is captured by
/// the root container and rooted there for the life of the process, so implementing the interface
/// creates the leak rather than closing it. See
/// <see href="https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines#disposable-transient-services-captured-by-container">
/// disposable transient services captured by container</see>, and dotnet/maui#7354, closed as
/// not-planned with that same guidance.
/// </para>
/// <para>
/// So nothing here outlives a navigation. A derived view model acquires its resources in
/// <see cref="NavigatedTo"/> and releases them in <see cref="NavigatedFrom"/>. Rebuilding on
/// return costs nothing worth counting: <see cref="Plugin.Maui.NearbyConnections.INearby.Devices"/>
/// is state, not a replayless event stream, so a fresh collection seeds itself with every device
/// found while the page was away.
/// </para>
/// </remarks>
[SuppressMessage(
    "Microsoft.Design",
    "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
    Justification = "The analyzer's fix — implementing IDisposable — is the defect here, not the cure: "
        + "the container roots every disposable transient it creates, and .NET MAUI never calls Dispose "
        + "on a binding context anyway. _navigationCts is disposed on every NavigatedFrom instead, so it "
        + "never outlives the navigation that created it. See the class remarks.")]
public abstract partial class BasePageViewModel(
    IDispatcher dispatcher)
    : ObservableObject
{
    CancellationTokenSource? _navigationCts;
    IDispatcherTimer? _relativeTimeTimer;
    EventHandler? _relativeTimeTick;

    protected IDispatcher Dispatcher { get; } = dispatcher;

    protected CancellationToken NavigationToken => _navigationCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Why the last start attempt failed, or <see langword="null"/> if none has.
    /// </summary>
    /// <remarks>
    /// Shown on the page rather than as an alert, for the same reason a failed device row keeps its
    /// reason inline: a modal steals the screen from the toggle the user is about to tap again.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    public partial string? FailureReason { get; set; }

    /// <summary>
    /// What the user can do about <see cref="FailureReason"/>, or <see langword="null"/> when there
    /// is nothing to suggest beyond trying again.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRemedy))]
    public partial string? FailureRemedy { get; set; }

    /// <summary>
    /// Whether the last start attempt failed and the page is showing its reason.
    /// </summary>
    public bool HasFailed => FailureReason is not null;

    /// <summary>
    /// Whether there is a remedy to show alongside <see cref="FailureReason"/>.
    /// </summary>
    public bool HasRemedy => !string.IsNullOrEmpty(FailureRemedy);

    /// <summary>
    /// Records why a start attempt failed, for the page to render.
    /// </summary>
    protected void Fail(string reason, string? remedy = null)
    {
        FailureReason = reason;
        FailureRemedy = remedy;
    }

    /// <summary>
    /// Clears a stale failure, so a fresh attempt shows what is happening now rather than what
    /// failed last time.
    /// </summary>
    protected void ClearFailure()
    {
        FailureReason = null;
        FailureRemedy = null;
    }

    protected void TrackRelativeTime(IReadOnlyList<NearbyDeviceViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StopRelativeTime();

        if (rows.Count == 0)
        {
            return;
        }

        _relativeTimeTick = (_, _) =>
        {
            foreach (var row in rows)
            {
                row.RefreshRelativeTime();
            }
        };

        _relativeTimeTimer = Dispatcher.CreateTimer();
        _relativeTimeTimer.Interval = TimeSpan.FromSeconds(30);
        _relativeTimeTimer.Tick += _relativeTimeTick;
        _relativeTimeTimer.Start();
    }

    protected void StopRelativeTime()
    {
        if (_relativeTimeTimer is null)
        {
            return;
        }

        _relativeTimeTimer.Stop();
        _relativeTimeTimer.Tick -= _relativeTimeTick;
        _relativeTimeTimer = null;
        _relativeTimeTick = null;
    }

    [RelayCommand]
    protected virtual void NavigatedTo()
    {
        var old = _navigationCts;
        _navigationCts = new CancellationTokenSource();
        old?.Cancel();
        old?.Dispose();
    }

    [RelayCommand]
    protected virtual void NavigatedFrom()
    {
        var old = _navigationCts;
        _navigationCts = null;
        old?.Cancel();
        old?.Dispose();

        StopRelativeTime();
        ClearFailure();
    }
}