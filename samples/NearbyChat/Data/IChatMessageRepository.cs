using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Data;

/// <summary>
/// Persistence for chat history.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Async by contract, even though the sample's implementation is in-memory.</strong> A
/// database-backed implementation (EF Core, SQLite) is inherently async, and a synchronous interface
/// would force every such implementation into <c>.Result</c>. The shape is the one a real repository
/// needs, so swapping the implementation requires no consumer changes.
/// </para>
/// <para>
/// <strong>Registered as a singleton here, which a real implementation cannot be.</strong> The
/// sample's store is a thread-safe in-memory dictionary with no per-operation state. An EF Core
/// <c>DbContext</c> is neither: it is a unit of work that is not thread-safe and must not be shared
/// across concurrent operations. Backing this with a database means registering the repository
/// <c>Scoped</c> and resolving one scope per operation, so that no long-lived service captures an
/// instance.
/// </para>
/// </remarks>
public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> GetAllAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    Task<ChatMessage> SaveAsync(NearbyDevice device, ChatMessage message, CancellationToken cancellationToken = default);

    Task ClearSessionAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}
