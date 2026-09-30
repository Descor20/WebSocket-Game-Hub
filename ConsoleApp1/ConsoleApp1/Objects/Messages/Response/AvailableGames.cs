using ConsoleApp1.Objects.Rooms.Games;

namespace ConsoleApp1.Objects.Messages.Response;

public class AvailableGames
{
    public List<GameLowDetails> Games { get; set; }

    public AvailableGames(List<GameLowDetails> games)
    {
        Games = games;
    }
}