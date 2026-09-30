namespace ConsoleApp1.Objects.Messages.Response;

public class Starting
{
    public string Message { get; set; }
    public string Id { get; set; } 
    public string GameId { get; set; } 

    public Starting(string id, string GameId)
    {
        Message = $"The lobby {id} is starting!";
        this.Id = id;
        this.GameId = GameId;
    }
}