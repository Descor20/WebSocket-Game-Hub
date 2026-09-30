using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Games;

public partial class BrotatoGame: BaseGame
{
    override
    public  async Task OnMessageReceived(string json)
    {
        Console.WriteLine(json);
        try
        {
            BaseMessage? msg = JsonSerializer.Deserialize<BaseMessage>(json);
            if (msg == null)
            {
                Console.WriteLine("Message is null");
                await InvokeAsync(StateHasChanged);
                return;
            }

            switch (msg.Type)
            {
                case "Subscribed":
                    Truth? info = null;
                    try
                    {
                        info = msg.Payload.Deserialize<Truth>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Subscription failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info is { Response: true })
                    {
                        _subscribed = true;
                    }
                    else
                    {
                        Console.WriteLine("Subscription failed");
                    }
                    break;
                
                case "Room:Command":
                    ControllerAction? info4 = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.ToString());
                        info4 = msg.Payload.Deserialize<ControllerAction>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("command room failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info4 != null)
                    {
                        Console.WriteLine("The Action didn't work");
                    }
                    else
                    {
                        if (info4 is null)
                        {
                            Console.WriteLine("No command found");
                            break;
                        }
                        
                        switch (info4.action)
                        {
                            case "start" :
                                //TODO: implement
                                break;
                            
                            default:
                                Console.WriteLine($"Unknown action: {info4.action}");
                                break;
                        }
                    }

                    break;
                
                default:
                    Console.WriteLine($"Unknown message type: {msg.Type}");
                    if (msg.Type == "error")
                    {
                        try
                        {
                            ErrorMessage? err = msg.Payload.Deserialize<ErrorMessage>();
                            if (err != null)
                            {
                                Console.WriteLine(err.message);
                            }
                        }
                        catch (Exception ex)
                        {}
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something unexpected happened");
        }

        await InvokeAsync(StateHasChanged);
    }
}