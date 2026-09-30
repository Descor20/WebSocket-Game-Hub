using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using GameHub.Components.Objects.Singleton;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages;

public partial class GameHubSelection : ComponentBase, IDisposable, Menu 
{
    [Parameter]
    public string Id { get; set; } = default!;
    
    [Inject] 
    private IJSRuntime JS { get; set; } = default!;
    
    [Inject] 
    private SessionData Session { get; set; }
    
    [Inject] 
    private SessionManager Manager { get; set; }
    
    [Inject] 
    private HubData _HubData { get; set; }
    
    [Inject] 
    private NavigationManager Navigation { get; set; }
    
    private bool _subscribed { get; set; } = false;
    private int _selected { get; set; } = 0;
    private AvailableGames? _availableGames = null;
    private GameDetail? _selectedGame = null;
    
    private IJSObjectReference? _module;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Console.WriteLine("test");
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
            content.Role = "Subscriber";
            
            Information content2 = new Information();
            content2.ID = Session.SessionId;

            GameInformation content3 = new GameInformation();
            content3.ID = Session.SessionId;
            content3.Selected = _selected;
            
            await _module!.InvokeVoidAsync("sendMessage",
                new { type = "Subscribed", room = Session.SubscribedId, payload = content });

            await _module!.InvokeVoidAsync("sendMessage",
                new { type = "Information:Games", room = Session.SubscribedId, payload = content2 });
            
            await _module!.InvokeVoidAsync("sendMessage",
                new { type = "Information:Game", room = Session.SubscribedId, payload = content3 });
        }
    }

    public void Dispose()
    {
        //TODO: Implement
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
                
                case "Information:Games":
                    AvailableGames? info2 = null;
                    try
                    {
                        Console.WriteLine("received message of games");
                        info2 = JsonDeserializer.Deserialize<AvailableGames>(msg.Payload.GetRawText());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("info games : error");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info2 is null)
                    {
                        Console.WriteLine("The Games could not load");
                    }
                    else
                    {
                        _availableGames = info2;
                        if (_HubData._GamesLow is null)
                        {
                            _HubData._GamesLow = info2;
                        }
                    }

                    break;
                
                case "Information:Game":
                    GameDetail? info3 = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.ToString());
                        info3 = msg.Payload.Deserialize<GameDetail>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("info game");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info3 is null)
                    {
                        Console.WriteLine("The Game detail could not load");
                    }
                    else
                    {
                        _selectedGame = info3;
                        if (!_HubData.recordedGames.Contains(info3.GameId))
                        {
                            _HubData.recordedGames.Add(info3.GameId);
                            _HubData._Games.Add(info3);
                        }
                    }

                    break;
                
                case "Room:Command":
                    ControllerAction? info4 = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.GetRawText());
                        info4 = JsonDeserializer.Deserialize<ControllerAction>(msg.Payload.GetRawText());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("command room failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info4 is null)
                    {
                        Console.WriteLine("The Action didn't work");
                    }
                    else
                    {
                        if (_availableGames is null)
                        {
                            Console.WriteLine("No available games could be found");
                            break;
                        }

                        if (info4 is null)
                        {
                            Console.WriteLine("No command found");
                            break;
                        }
                        GameInformation content = new GameInformation();
                        content.ID = Session.SessionId;
                        switch (info4.action)
                        {
                            case "left":
                                if (_selected <= 0)
                                {
                                    _selected = _availableGames.Games.Count - 1;
                                }
                                else
                                {
                                    _selected--;
                                }
                                break;
                            
                            case "right":
                                if (_selected >= _availableGames.Games.Count - 1)
                                {
                                    _selected = 0;
                                }
                                else
                                {
                                    _selected++;
                                }
                                break;
                            
                            case "select" :
                                content.Selected = _selected;
                                await _module!.InvokeVoidAsync("sendMessage", new {type = "Information:Game", room=Session.SubscribedId, payload=content});
                                break;
                            
                            default:
                                Console.WriteLine($"Unknown action: {info4.action}");
                                break;
                        }
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
                        if (!(_selectedGame is null))
                        {
                            string roomId = info5.Id;
                            await _module!.InvokeVoidAsync("sendMessage",
                                new { type = "SwitchRoom", room = roomId, payload = new { } });

                            Session.SubscribedId = roomId;

                            Navigation.NavigateTo($"/GameHub/Game/{roomId}/{_selectedGame.Details.Name}");
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
}