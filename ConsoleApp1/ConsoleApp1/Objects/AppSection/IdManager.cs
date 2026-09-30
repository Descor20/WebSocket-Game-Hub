using System.Runtime.CompilerServices;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Rooms;
using ConsoleApp1.Objects.User;

namespace ConsoleApp1.Objects.AppSection;

public class IdManager
{
    
    public static List<Listener> Listeners = new List<Listener>();
    public static List<User.User> Users { get; set; } = new List<User.User>();
    public static List<Lobby> Lobbies { get; set; } = new List<Lobby>();
    public static List<Game> Games { get; set; } = new List<Game>();
    
    public static EntryPage? EntryPage { get; set; }
    public static Dictionary<string, string> Registration { get; set; } = new Dictionary<string, string>();

    public static void AddUser(User.User user)
    {
        Users.Add(user);
    }
    
    public static void RemoveUser(User.User user)
    {
        Users.Remove(user);
    }

    public static bool ExistUser(User.User user)
    {
        return Users.Contains(user);
    }

    public static string? RegisterUser(User.User user, string room)
    {
        if (!Users.Contains(user))
        {
            return null;
        }
        try
        {
            string roomId = Registration[user.Id];
            return roomId;
        }
        catch (Exception)
        {
            Registration[user.Id] = room;
            return room;
        }
    }

    public static bool UnregisterUser(User.User user, string room)
    {
        if (!Users.Contains(user))
        {
            return false;
        }
        try
        {
            return Registration.Remove(user.Id);
        }
        catch (Exception)
        {
            Registration[user.Id] = room;
            return false;
        }
    }

    public static string? GetRegistration(string playerId)
    {
        try
        {
            return Registration[playerId];
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Listener? GetListener(string roomId)
    {
        if (Listeners.Count != 0)
        {
            foreach (Listener listener in Listeners)
            {
                if (listener.Id == roomId)
                {
                    return listener;
                }
            }
        }
        return null;
    }

    public static AvailableRooms GetAvailableRooms()
    {
        AvailableRooms res = new AvailableRooms();
        foreach (Lobby lobby in Lobbies)
        {
            res.Lobbies.Add(lobby.Id);
        }
        foreach (Game game in Games)
        {
            res.Games.Add(game.Id);
        }

        if (EntryPage != null)
        {
            res.EntryPoint.Add(EntryPage.Id);
        }

        return res;
    }
}