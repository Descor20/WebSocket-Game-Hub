using System.Text.Json;
using ConsoleApp1.Objects.AppSection;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Rooms.Games;
using ConsoleApp1.Objects.User;
using ConsoleApp1.Objects.Utils;

namespace ConsoleApp1.Objects.Rooms;

public class Game : Listener
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
    private readonly List<Player> _allPlayers = new List<Player>();

    private readonly int _selected;
    public string GameId { get; set; }
    public Base SelectedGame { get; set; }

    /* ---- Constructor ---- */
    public Game(Server server, int selectedGame) : base(server)
    {
        _selected = selectedGame;
        SelectedGame = GameManager.GetGame(selectedGame);
        GameId = SelectedGame!.GameId;
        
        IdManager.Games.Add(this);
        IdManager.Listeners.Add(this);
    }
    
    /* ---- Field Manager ---- */
    override 
    public void AddPlayer(User.User player)
    {
        Player? p = player as Player;
        if (p != null) AddPlayer(p);
    }

    override
    public void RemovePlayer(User.User player)
    {
        Player? p = player as Player;
        if (p != null) RemovePlayer(p);
    }
    
    public void AddPlayer(Player player)
    {
        lock (this._lock) _allPlayers.Add(player);
    }

    public void RemovePlayer(Player player)
    {
        lock (_lock)
        {
            IdManager.UnregisterUser(player, this.Id);
            _allPlayers.Remove(player);
            //LeaveRoom(player);
        }
    }
    
    /* ---- Lobby Manager ---- */
    public void CloseGame()
    {
        lock (this._lock)
        {
            foreach (Player player in _allPlayers)
            {
                _ = player.SendAsync("closing", new Closing(this.Id));
                RemovePlayer(player);
            }
        }

        IdManager.Games.Remove(this);
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
        //Console.WriteLine("Got a message !");

        switch (msg.Type)
        {
            case "Connect":
            {
                Connect? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Connect>(msg.Payload.GetRawText());                }
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
                    info = JsonDeserializer.Deserialize<Disconnect>(msg.Payload.GetRawText());                }
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
                
            case "Room:Command":
            {
                RoomAction? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<RoomAction>(msg.Payload.GetRawText());                }
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

                switch (info.Action)
                {
                  default:
                        foreach (User.User subscriber in Subscribers)
                        {
                            await subscriber.SendAsync("Room:Command", new { action = info.Action, from = player.Id });
                        }
                        break;
                }

            }
                return;
        }
    }
}