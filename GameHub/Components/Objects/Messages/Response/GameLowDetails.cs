using System.Text.Json.Serialization;

namespace ConsoleApp1.Objects.Messages.Response;

public class GameLowDetails
{
    [JsonConstructor]
    public GameLowDetails()
    {
    }
    
    public GameLowDetails(string gameId, string gameName, int maxPlayers, string image_path)
    {
        GameId = gameId;
        GameName = gameName;
        MaxPlayers = maxPlayers;
        
        Image = LoadImageAsBase64(image_path);
    }

    public string GameId { get; set; }
    public string GameName { get; set; }
    public int MaxPlayers { get; set; }
    public string Image { get; set; }
    
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