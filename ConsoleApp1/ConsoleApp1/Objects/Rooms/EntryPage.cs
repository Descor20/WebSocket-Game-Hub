using System.Text.Json;
using ConsoleApp1.Objects.AppSection;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.User;
using ConsoleApp1.Objects.Utils;

namespace ConsoleApp1.Objects.Rooms;

public class EntryPage : Listener
{
    /*
     * ---- Inherited ----
     *
     * Id: The ID of the Listener (Lobby/Game).
     * _lock: The lock that allow to interact with objects.
     * _server: The server which launch this object.
     * Subscribers: The list of User which has subscribed to the room.
     */
    
    /* ---- Attributes ---- */
    private readonly List<User.User> _allPlayers = new List<User.User>();
    
    /* ---- Constructor ---- */
    public EntryPage(Server server) : base(server)
    {
        IdManager.EntryPage = this;
        IdManager.Listeners.Add(this);
    }
    
    /* ---- Field Manager ---- */
    override 
    public void AddPlayer(User.User player)
    {
        lock (this._lock) _allPlayers.Add(player);
    }

    override 
    public void RemovePlayer(User.User player)
    {
        lock (_lock)
        {
            IdManager.UnregisterUser(player, this.Id);
            _allPlayers.Remove(player);
        }
    }
    
    /* ---- Messages Handler ---- */
    override 
    public async Task HandleMessageAsync(User.User player, string json)
    {
        BaseMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<BaseMessage>(json);
        }
        catch
        {
            await player.SendAsync("error", new { message = "invqlid JSON" });
            return;
        }

        if (msg is null) return;
        Console.WriteLine("Got a message !");

        switch (msg.Type)
        {
            case "Connect":
            {
                Connect? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Connect>(msg.Payload.GetRawText());
                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Incorrect payload: {msg.Payload}" });
                    return;
                }

                if (info is null)
                {
                    await player.SendAsync("error", new { message = $"Empty payload: {msg.Payload}" });
                    return;
                }

                if (player.Id != info.ID)
                {
                    await player.SendAsync("error", new { message = $"Invalid player ID: {info.ID}" });
                    return;
                }

                if (IdManager.ExistUser(player))
                {
                    await player.SendAsync("error", new { message = $"Player already exists: {info.ID}" });
                    return;
                }

                try
                {
                    IdManager.AddUser(player);
                    await player.SendAsync("Connect", new Truth(true));
                }
                catch (Exception)
                {
                    await player.SendAsync("Connect", new Truth(false));
                }

                break;
            }

            case "Disconnect":
            {
                Disconnect? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Disconnect>(msg.Payload.GetRawText());
                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Incorrect payload: {msg.Payload}" });
                    return;
                }

                if (info is null)
                {
                    await player.SendAsync("error", new { message = $"Empty payload: {msg.Payload}" });
                    return;
                }

                if (player.Id != info.ID)
                {
                    await player.SendAsync("error", new { message = $"Invalid player ID: {info.ID}" });
                    return;
                }

                if (!IdManager.ExistUser(player))
                {
                    await player.SendAsync("error", new { message = $"Player don't exists: {info.ID}" });
                    return;
                }

                try
                {
                    IdManager.RemoveUser(player);
                    await player.SendAsync("Disconnect", new Truth(true));
                }
                catch (Exception)
                {
                    await player.SendAsync("Disconnect", new Truth(false));
                }

                break;
            }

            case "Information:Rooms":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());
                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Incorrect payload: {msg.Payload}" });
                    return;
                }

                if (info is null)
                {
                    await player.SendAsync("error", new { message = $"Empty payload: {msg.Payload}" });
                    return;
                }

                if (!IdManager.ExistUser(player) || player.Id != info.ID)
                {
                    await player.SendAsync("error", new { message = $"Invalid player ID: {info.ID}" });
                    return;
                }

                AvailableRooms rooms = IdManager.GetAvailableRooms();
                await player.SendAsync("Information:Rooms", rooms);
                break;
            }

            case "CreateRoom":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());
                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Incorrect payload: {msg.Payload}" });
                    return;
                }

                if (info is null)
                {
                    await player.SendAsync("error", new { message = $"Empty payload: {msg.Payload}" });
                    return;
                }

                if (!IdManager.ExistUser(player) || player.Id != info.ID)
                {
                    await player.SendAsync("error", new { message = $"Invalid player ID: {info.ID}" });
                    return;
                }

                Lobby room = new Lobby(this._server);
                foreach (User.User user in _allPlayers)
                {
                    await user.SendAsync("CreateRoom", new {RoomId = room.Id});
                }
                break;
            }
                
            case "Subscribed":
            {
                Subscription? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Subscription>(msg.Payload.GetRawText());                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Incorrect payload: {msg.Payload}" });
                    return;
                }

                if (info is null)
                {
                    await player.SendAsync("error", new { message = $"Empty payload: {msg.Payload}" });
                    return;
                }

                if (!IdManager.ExistUser(player) || player.Id != info.ID)
                {
                    await player.SendAsync("error", new { message = $"Invalid player ID: {info.ID}" });
                    return;
                }

                try
                {
                    AvailableRooms rooms = IdManager.GetAvailableRooms();
                    if (!(rooms.Lobbies.Contains(info.Room) || rooms.Games.Contains(info.Room)))
                    {
                        await player.SendAsync("error", new { message = $"The room doesn't exist: {info.Room}" });
                        return;
                    }
                    else
                    {
                        string? id = IdManager.RegisterUser(player, info.Room);
                        if (info.Role == "Subscriber")
                        {
                            Subscribers.Add(player);
                        }

                        if (id is null)
                        {
                            await player.SendAsync("Subscribed", new Truth(false));
                            return;
                        }
                        else
                        {
                            await player.SendAsync("Subscribed", new Truth(true));
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    await player.SendAsync("error", new { message = $"Something unexpected happened" });
                    return;
                }
            }

            default:
                await player.SendAsync("error", new { message = $"Message type unknown: {msg.Type}" });
                break;
        }
    }
}