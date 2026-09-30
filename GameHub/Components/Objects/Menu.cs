using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace GameHub.Components.Objects;

public interface Menu
{
    public abstract Task OnMessageReceived(string json);

    public abstract Task OnConnected();

    public abstract Task OnDisconnected(int code, string reason);
    
    public abstract Task OnError(string message);

}