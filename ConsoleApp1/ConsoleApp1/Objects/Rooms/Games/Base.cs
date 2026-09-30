using System.Text.Json;
using ConsoleApp1.Objects.Rooms.Games.Details;
using ConsoleApp1.Objects.Utils;

namespace ConsoleApp1.Objects.Rooms.Games;

public class Base
{
    public string GameFile { get; }
    public string GameId { get; }
    public Info GameDetails { get; }
    
    public Base(string path)
    {
        this.GameFile = path;

        Info? tmp = JsonDeserializer.DeserializeFromPath<Info>(path);
        GameDetails = tmp ?? throw new NullReferenceException();
        this.GameId = GameDetails.ID;
    }

}