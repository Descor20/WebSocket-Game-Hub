namespace GameHub.Components.Objects.Scoped;

public class SessionData
{
    
    public string Url { get; set; } = "ws://localhost:8080";
    public string? SessionId { get; set; }
    public string? SubscribedId { get; set; }
    
    public string? UserName { get; set; }
    public string? Mode { get; set; }
}
