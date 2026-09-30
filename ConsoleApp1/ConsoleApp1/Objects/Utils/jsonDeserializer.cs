using System.Text.Json;

namespace ConsoleApp1.Objects.Utils;

public class JsonDeserializer
{
    public static T? Deserialize<T>(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        T? tmp = JsonSerializer.Deserialize<T>(json, options);
        
        return tmp;
    }
    
    public static T? DeserializeFromPath<T>(string path)
    {
        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        T? tmp = JsonSerializer.Deserialize<T>(json, options);
        
        return tmp;
    }
}