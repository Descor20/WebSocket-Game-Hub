using ConsoleApp1.Objects.Rooms.Games;
using ConsoleApp1.Objects.Rooms.Games.Details;

namespace ConsoleApp1.Objects.Messages.Response;

public class GameDetail
{
    public string GameId { get; set; }
    public General Details { get; set; }
    public Controller Controllers { get; set; }
    public GameDetail(Base game)
    {
        this.GameId = game.GameId;
        this.Details = game.GameDetails.General;
        this.Controllers = game.GameDetails.Controller;
        
        //this.Details.Icon = LoadImageAsBase64(this.Details.Icon);
    }
    
    private static string LoadImageAsBase64(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        byte[] bytes = File.ReadAllBytes(path);
        string base64 = Convert.ToBase64String(bytes);
        string mimeType = GetMimeType(path);

        return $"data:{mimeType};base64,{base64}";
    }
    
    private static string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}