namespace ConsoleApp1.Objects.Rooms.Games.Details;

public class General
{
    public string Name { get; set; }
    public string GameType { get; set; }
    public string Description { get; set; }
    
    public string Icon { get; set; } 
    
    public List<string> Tags { get; set; }
    
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
}