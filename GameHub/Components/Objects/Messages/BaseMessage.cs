using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleApp1.Objects.Messages;

public class BaseMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}

/*
 * Multiple Type exists :
 * Information:{code}
 *   -> 0: Available Room
 * Subscription
 * Connect
 * Disconnect
 * Room:Command
*/