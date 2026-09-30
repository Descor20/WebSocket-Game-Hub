using System.Net.WebSockets;

namespace ConsoleApp1.Objects.User;

public class Player : User
{
    public string UserName { get; set; }
    
    public Player(string id, WebSocket socket) : base(id, socket)
    {
    }
}