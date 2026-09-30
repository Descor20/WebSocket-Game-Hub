using System.Text.Json;
using ConsoleApp1.Objects.AppSection;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Rooms.Games;
using ConsoleApp1.Objects.User;
using ConsoleApp1.Objects.Utils;

namespace ConsoleApp1.Objects.Rooms;

public class Lobby : Listener
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

    public int preSelect {get; set;}
    public int Selected { get; set; }

    /* ---- Constructor ---- */
    public Lobby(Server server) : base(server)
    {
        preSelect = 0;
        Selected = 0;
        
        IdManager.Lobbies.Add(this);
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
            //LeaveRoom(player);
        }
    }

    /* ---- Lobby Manager ---- */
    public void closeLobby()
    {
        lock (this._lock)
        {
            foreach (User.User player in _allPlayers)
            {
                _ = player.SendAsync("closing", new Closing(this.Id));
                RemovePlayer(player);
            }
        }

        IdManager.Lobbies.Remove(this);
    }

    private async Task StartGame()
    {
        Console.WriteLine("Starting game...");
        Console.WriteLine($"Selection menu is : {Selected} / {preSelect}");
        Game game = new Game(this._server, this.preSelect);
        Console.WriteLine($"The selected Game is {game.SelectedGame.GameDetails.General.Name}");
        foreach (User.User player in _allPlayers)
        {
            await player.SendAsync("start", new Starting(game.Id, game.GameId));
        }
        Selected = 0;
        preSelect = 0;
    }
    
    
    /* ---- Messages Handler ---- */
    override 
    public async Task HandleMessageAsync(User.User player, string json)
    {
        Console.WriteLine("Got a message: " + json);
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
            
            case "Information:Rooms":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());                }
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
            
            case "Information:Games":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());                }
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
                
                AvailableGames games = GameManager.GetAvailableGames();
                await player.SendAsync("Information:Games", games);
                break;
            }
            
            case "Information:Game":
            {
                GameInformation? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<GameInformation>(msg.Payload.GetRawText());                }
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
                
                Base game = GameManager.GetGame(info.Selected);
                await player.SendAsync("Information:Game", new GameDetail(game));
                break;
            }
            
            case "Information:Players":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());                }
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

                var players = IdManager.Users;
                await player.SendAsync("Information:Players", players);
                break;
            }
                
            case "Information:Registration":
            {
                Information? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Information>(msg.Payload.GetRawText());                }
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

                var registration = IdManager.Registration;
                await player.SendAsync("Information:Registration", registration);
                break;
            }
            
            case "Subscribed":
            {
                Subscription? info = null;
                try
                {
                    info = JsonDeserializer.Deserialize<Subscription>(msg.Payload.GetRawText());
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
                    case "start":
                        foreach (User.User subscriber in Subscribers)
                        {
                            await subscriber.SendAsync("Room:Command", new {action="select"});
                        }
                        Console.WriteLine("Starting room");
                        await StartGame();
                        break;
                    case "left":
                        preSelect = GameManager.MoveSelected(Selected, "left");
                        foreach (User.User subscriber in Subscribers)
                        {
                            await subscriber.SendAsync("Room:Command", new {action="left"});
                        }
                        break;
                    case "right":
                        preSelect = GameManager.MoveSelected(Selected, "right");
                        foreach (User.User subscriber in Subscribers)
                        {
                            await subscriber.SendAsync("Room:Command", new {action="right"});
                        }
                        Console.WriteLine("");
                        break;
                    case "select" :
                        Selected = preSelect;
                        Console.WriteLine("Select is not yet implemented");
                        foreach (User.User subscriber in Subscribers)
                        {
                            await subscriber.SendAsync("Room:Command", new {action="select"});
                        }
                        break;
                    default:
                        Console.WriteLine($"Unknown action: {info.Action}");
                        await player.SendAsync("error", new { message = $"Unknown action: {info.Action}" });
                        break;
                }
                break;
            }
            
            default:
                await player.SendAsync("error", new { message = $"Message type unknown: {msg.Type}" });
                break;
        }
    }

}