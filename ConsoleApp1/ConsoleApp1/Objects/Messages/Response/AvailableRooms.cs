namespace ConsoleApp1.Objects.Messages.Response;

public class AvailableRooms
{
    public List<string> EntryPoint { get; set; }
    public List<string> Lobbies {get; set;}
    public List<string> Games {get; set;}

    public AvailableRooms()
    {
        Lobbies = new List<string>();
        Games = new List<string>();
        EntryPoint = new List<string>();
    }
}