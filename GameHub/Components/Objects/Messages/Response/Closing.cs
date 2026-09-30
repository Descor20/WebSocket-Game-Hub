namespace ConsoleApp1.Objects.Messages.Response;

public class Closing
{
    public string Message { get; set; }
    public string Id { get; set; } 

    public Closing(string id)
    {
        Message = $"The lobby {id} is closing!";
        this.Id = id;
    }
}