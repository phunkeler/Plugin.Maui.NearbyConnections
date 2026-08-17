namespace NearbyChat.Services;

/// <summary>
/// Wraps an <see cref="IDispatcherTimer"/> that periodically invokes a callback while active,
/// idempotently. Driven by <c>BasePageViewModel.TrackRelativeTime</c>, which is the only caller.
/// </summary>
public sealed class RelativeTimeTicker(
    IDispatcher dispatcher,
    TimeSpan interval,
    Action onTick)
{
    IDispatcherTimer? _timer;

    public void SetActive(bool active)
    {
        if (active)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = interval;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    void Stop()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    void OnTick(object? sender, EventArgs e) => onTick();
}
