using System.ComponentModel;
using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using GameHub.Components.Objects.Singleton;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Controller;

public class BaseController : ComponentBase, IDisposable, Menu 
{
    [Parameter]
    public string Id { get; set; } = default!;
    
    [Inject] 
    protected NavigationManager Navigation { get; set; }
    
    [Inject] 
    protected IJSRuntime JS { get; set; } = default!;
    
    [Inject] 
    protected SessionData Session { get; set; }
    
    [Inject] 
    protected SessionManager Manager { get; set; }
    
    [Inject] 
    private HubData _HubData { get; set; }
    
    protected bool _subscribed { get; set; } = false;
    
    protected IJSObjectReference? _module;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (Id != Session.SubscribedId)
            {
                Console.WriteLine($"User {Session.SessionId} did not subscribe to the selected GameHub");
                Navigation.NavigateTo($"/GameHub/");
                return;
            }

            if (Session.SessionId is null)
            {
                Console.WriteLine($"User {Session.SessionId} did not connect to the websocket");
                Navigation.NavigateTo($"/GameHub/");
                return;
            }

            Manager._dotNetRef = DotNetObjectReference.Create<Menu>(this);
            _module = Manager._module;

            Subscription content = new Subscription();
            content.ID = Session.SessionId;
            content.Room = Session.SubscribedId;
            content.Role = "Player";
            
            await _module!.InvokeVoidAsync("sendMessage",
                new { type = "Subscribed", room = Session.SubscribedId, payload = content });
        }
        else
        {
            _module = Manager._module;
        }
    }
    
    public async Task OnConnected()
    {
        Console.WriteLine("No new stuff here");
        throw new NotImplementedException();
    }

    public async Task OnError(string message)
    {
        Console.WriteLine($"Error WS : {message}");
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnDisconnected(int code, string reason)
    {
        Console.WriteLine($"Disconnected : {code} - {reason}");
        Navigation.NavigateTo($"/GameHub/");
    }
    
    public async Task OnMessageReceived(string json)
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
                
                case "start" :
                    Starting? info5 = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.GetRawText());
                        info5 = JsonDeserializer.Deserialize<Starting>(msg.Payload.GetRawText());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("command room failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info5 is null)
                    {
                        Console.WriteLine("The Starting is empty");
                    }
                    else
                    {
                        string roomId = info5.Id;
                        await _module!.InvokeVoidAsync("sendMessage",
                            new { type = "SwitchRoom", room = roomId, payload = new { } });
                        
                        Session.SubscribedId = roomId;

                        if (_HubData.recordedGames.Contains(info5.GameId))
                        {
                            foreach (GameDetail game in _HubData._Games)
                            {
                                if (game.GameId == info5.GameId)
                                {
                                    Navigation.NavigateTo($"/GameHub/Room/{roomId}/Player/{game.Controllers.Player}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Game {info5.GameId} was not found");
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
    
    public void Dispose()
    {
        //TODO: Implement
    }
}