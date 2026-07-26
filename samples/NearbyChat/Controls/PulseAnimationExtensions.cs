namespace NearbyChat.Controls;

/// <summary>
/// A looping fade pulse (1 → 0.4 → 1) for "actively broadcasting/scanning"
/// visual indicators, built on <see cref="Animation"/>'s own repeat support —
/// no owned <see cref="CancellationTokenSource"/>, no fire-and-forget task.
/// </summary>
public static class PulseAnimationExtensions
{
    const string AnimationName = "Pulse";

    public static void StartPulse(this VisualElement element)
    {
        var animation = new Animation();
        animation.WithConcurrent(v => element.Opacity = v, start: 1, end: 0.4, beginAt: 0, finishAt: 0.5);
        animation.WithConcurrent(v => element.Opacity = v, start: 0.4, end: 1, beginAt: 0.5, finishAt: 1);

        animation.Commit(
            element,
            AnimationName,
            length: 1600,
            easing: Easing.CubicInOut,
            repeat: () => true);
    }

    public static void StopPulse(this VisualElement element)
    {
        element.AbortAnimation(AnimationName);
        element.Opacity = 1;
    }
}
