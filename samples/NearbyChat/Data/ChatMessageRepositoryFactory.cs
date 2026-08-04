using Microsoft.Extensions.DependencyInjection;

namespace NearbyChat.Data;

/// <summary>
/// The default <see cref="IChatMessageRepositoryFactory"/>, backed by <see cref="IServiceScopeFactory"/>.
/// </summary>
/// <remarks>
/// This adapter is deliberately the only type in the app that resolves a service by type at runtime.
/// Confining that to a single composition-root class keeps every consumer on constructor injection:
/// they depend on <see cref="IChatMessageRepositoryFactory"/>, which is substitutable in tests
/// without a container.
/// </remarks>
public sealed class ChatMessageRepositoryFactory(IServiceScopeFactory scopeFactory)
    : IChatMessageRepositoryFactory
{
    readonly IServiceScopeFactory _scopeFactory = scopeFactory
        ?? throw new ArgumentNullException(nameof(scopeFactory));

    public IChatMessageRepositoryHandle Create()
    {
        var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return new Handle(scope, scope.ServiceProvider.GetRequiredService<IChatMessageRepository>());
        }
        catch
        {
            // Resolution threw, so the caller never receives a handle and can never dispose the
            // scope. Release it here or the scope leaks.
            _ = scope.DisposeAsync().AsTask();
            throw;
        }
    }

    sealed class Handle(AsyncServiceScope scope, IChatMessageRepository repository)
        : IChatMessageRepositoryHandle
    {
        public IChatMessageRepository Repository { get; } = repository;

        public ValueTask DisposeAsync() => scope.DisposeAsync();
    }
}
