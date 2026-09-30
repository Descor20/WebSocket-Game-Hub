using ConsoleApp1.Objects.Messages.Response;

namespace GameHub.Components.Objects.Singleton;

public class HubData
{
    public AvailableGames? _GamesLow { get; set; } = null;
    public List<GameDetail> _Games { get; set; } = new List<GameDetail>();
    
    public List<string> recordedGames { get; set; } = new List<string>();
}