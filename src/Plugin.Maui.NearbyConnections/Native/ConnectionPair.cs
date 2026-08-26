namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One live link in the bridge's connection table: the public <see cref="NearbyConnection"/> and
/// the <see cref="IPlatformConnection"/> behind it, held together so release can dispose both in
/// order (contract C7) and so the fact "device X has a live connection" has one owner with both
/// halves (the C5 table in <c>docs/ARCHITECTURE.md</c> section 4).
/// </summary>
/// <param name="Connection">The public connection the application holds.</param>
/// <param name="Platform">The platform link the connection sends through.</param>
sealed record ConnectionPair(NearbyConnection Connection, IPlatformConnection Platform);
