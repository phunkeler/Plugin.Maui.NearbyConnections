using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Data;

/// <summary>
/// Persistence for chat history, scoped to a single unit of work.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Async by contract, even though the sample's implementation is in-memory.</strong> A
/// database-backed implementation (EF Core, SQLite) is inherently async, and a synchronous interface
/// would force every such implementation into <c>.Result</c>. The shape is the one a real repository
/// needs, so swapping the implementation requires no consumer changes.
/// </para>
/// <para>
/// <strong>Registered scoped, never singleton.</strong> A real implementation wraps a unit of work
/// (an EF Core <c>DbContext</c>) that is not thread-safe and must not be shared across concurrent
/// operations. Resolve one per unit of work through <see cref="IChatMessageRepositoryFactory"/>
/// rather than holding an instance in a long-lived service.
/// </para>
/// </remarks>
public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> GetAllAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    Task<ChatMessage> SaveAsync(NearbyDevice device, ChatMessage message, CancellationToken cancellationToken = default);

    Task ClearSessionAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}
