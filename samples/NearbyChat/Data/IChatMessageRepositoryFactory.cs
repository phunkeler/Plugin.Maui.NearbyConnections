namespace NearbyChat.Data;

/// <summary>
/// Creates a scoped <see cref="IChatMessageRepository"/> for one unit of work.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that lets a singleton (the ingestion service, a ViewModel) use a scoped
/// repository without capturing one. Holding an <see cref="IChatMessageRepository"/> in a singleton
/// field is the classic captive-dependency bug: with EF Core it pins one <c>DbContext</c> for the
/// life of the app, shares it across concurrent operations that must not share it, and accumulates
/// tracked entities that are never released.
/// </para>
/// <para>
/// Consumers depend on this interface rather than on <see cref="IServiceProvider"/>, so no code
/// outside the composition root performs service location.
/// </para>
/// </remarks>
public interface IChatMessageRepositoryFactory
{
    /// <summary>
    /// Opens a new unit of work. Dispose the returned handle to release the underlying scope.
    /// </summary>
    IChatMessageRepositoryHandle Create();
}

/// <summary>
/// A repository bound to a DI scope that lives until the handle is disposed.
/// </summary>
public interface IChatMessageRepositoryHandle : IAsyncDisposable
{
    IChatMessageRepository Repository { get; }
}
