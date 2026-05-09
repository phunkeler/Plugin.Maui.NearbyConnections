using System.Collections.Specialized;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface IChatMessageService
{
    void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message);
    Task SendChatMessage(NearbyDevice device, ChatMessage message);
}

public class ChatMessageService : IChatMessageService
{
    readonly IChatMessageRepository _repository;
    readonly IMessenger _messenger;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;

    public ChatMessageService(
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        IChatMessageRepository repository,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(messenger);

        _advertiser = advertiser;
        _discoverer = discoverer;
        _repository = repository;
        _messenger = messenger;

        if (_advertiser.ActiveConnections is INotifyCollectionChanged advertiserNotify)
            advertiserNotify.CollectionChanged += OnActiveConnectionsChanged;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged discovererNotify)
            discovererNotify.CollectionChanged += OnActiveConnectionsChanged;
    }

    public void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message)
    {
        _repository.Save(device, message);
        _messenger.Send(new ChatMessageReceived(device, message));
    }

    public async Task SendChatMessage(NearbyDevice device, ChatMessage message)
    {
        _repository.Save(device, message);

        var conn = FindConnection(device);
        if (conn is null)
            return;

        if (message.Attachments.FirstOrDefault() is MediaAttachment mediaAttachment)
        {
            if (!string.IsNullOrWhiteSpace(mediaAttachment.FilePath))
            {
                await conn.SendAsync(mediaAttachment.FilePath);
            }
        }
        else if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await conn.SendAsync(Encoding.UTF8.GetBytes(message.Text));
        }
    }

    NearbyConnection? FindConnection(NearbyDevice device)
    {
        var advertiserConn = _advertiser.ActiveConnections
            .FirstOrDefault(c => c.RemoteDevice.Id == device.Id);
        if (advertiserConn is not null)
            return advertiserConn;

        return _discoverer.ActiveConnections
            .FirstOrDefault(c => c.RemoteDevice.Id == device.Id);
    }

    void OnActiveConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
        {
            foreach (NearbyConnection conn in e.OldItems)
            {
                _repository.ClearSession(conn.RemoteDevice);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Cannot determine which devices were removed on Reset; no-op for session cleanup.
        }
    }
}
