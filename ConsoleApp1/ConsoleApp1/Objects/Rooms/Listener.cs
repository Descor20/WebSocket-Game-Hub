using ConsoleApp1.Objects.AppSection;
using ConsoleApp1.Objects.User;

namespace ConsoleApp1.Objects.Rooms;

public abstract class Listener
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    protected readonly object _lock = new();
    protected Server _server;
    
    protected List<User.User> Subscribers = new List<User.User>();

    public Listener(Server server)
    {
        this._server = server;
    }

    /* ---- Field Manager ---- */
    public abstract void AddPlayer(User.User player);

    public abstract void RemovePlayer(User.User player);
    public abstract Task HandleMessageAsync(User.User player, string json);
}