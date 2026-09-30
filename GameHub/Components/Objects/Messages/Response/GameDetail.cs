using System.Text.Json.Serialization;
using ConsoleApp1.Objects.Rooms.Games;
using ConsoleApp1.Objects.Rooms.Games.Details;

namespace ConsoleApp1.Objects.Messages.Response;

public class GameDetail
{
    [JsonConstructor]
    public GameDetail()
    {
    }

    public GameDetail(string gameId, General details, Controller controllers)
    {
        GameId = gameId;
        Details = details;
        Controllers = controllers;
    }

    public string GameId { get; set; }
    public General Details { get; set; }
    public Controller Controllers { get; set; }
}