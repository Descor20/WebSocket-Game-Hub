using System.Text.Json.Serialization;

namespace ConsoleApp1.Objects.Messages.Response;

public class ControllerAction
{
    public string action { get; set; }
    
    public string? from { get; set; }


    [JsonConstructor]
    public ControllerAction()
    {
    }
}