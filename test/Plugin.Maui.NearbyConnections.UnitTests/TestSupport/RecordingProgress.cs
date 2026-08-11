namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Captures reported progress so ordering and content can be asserted.
/// </summary>
/// <remarks>
/// Hand-written rather than substituted: the assertion is on the <em>sequence</em> of reports, which
/// reads as a list comparison and would otherwise be spelled as a series of ordered mock
/// verifications.
/// </remarks>
sealed class RecordingProgress : IProgress<NearbyTransferProgress>
{
    public List<NearbyTransferProgress> Reports { get; } = [];

    public void Report(NearbyTransferProgress value) => Reports.Add(value);
}
