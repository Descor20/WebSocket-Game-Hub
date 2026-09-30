using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ConsoleApp1.Objects.User;

public class User
{
    public string Id { get; }
    public WebSocket Socket { get; }

    public User(String id, WebSocket socket)
    {
        this.Id = id;
        this.Socket = socket;
    }
    
    public async Task SendAsync(string type, object payload)
    {
        if (Socket.State != WebSocketState.Open) return;

        var envelope = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["payload"] = payload
        };

        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);
        await Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}