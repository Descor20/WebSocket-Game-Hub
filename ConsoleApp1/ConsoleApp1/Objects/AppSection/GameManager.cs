

using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Rooms;
using ConsoleApp1.Objects.Rooms.Games;

namespace ConsoleApp1.Objects.AppSection;

public class GameManager
{
    public static List<Base> games = new List<Base>();

    public static void AddGames()
    {
        Base mario = new Mario();
        games.Add(mario);
        
        Base brotato = new Brotato();
        games.Add(brotato);
        
        Base chess = new Chess();
        games.Add(chess);
        
        //Here you can add more games
    }

    public static AvailableGames GetAvailableGames()
    {
        List<GameLowDetails> res = new List<GameLowDetails>();
        for (int i = 0; i < games.Count; i++)
        {
            Base game = games[i];
            res.Add(new GameLowDetails(game.GameId, game.GameDetails.General.Name, game.GameDetails.General.MaxPlayers, game.GameDetails.General.Icon));
        }
        return new AvailableGames(res);
    }

    public static Base GetGame(int i)
    {
        return games[i];
    }

    public static int MoveSelected(int i, string direction)
    {
        switch (direction)
        {
            case "left":
                if (i <= 0)
                {
                    return games.Count - 1;
                }
                return i - 1;
            case "right":
                if (i >= games.Count - 1)
                {
                    return 0;
                }
                return i + 1;
        }

        return 0;
    }
}