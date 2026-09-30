using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages;

public partial class GameHub : ComponentBase, IDisposable, Menu
{
    [Inject] 
    private IJSRuntime JS { get; set; } = default!;
    
    [Inject] 
    private SessionData Session { get; set; }
    
    [Inject] 
    private SessionManager Manager { get; set; }
    
    [Inject] 
    private NavigationManager Navigation { get; set; }
    
    public string Url { get; set; } = "ws://localhost:8080/";
    private readonly CancellationTokenSource _cts = new();
    
    private bool _connected = false;
    private bool _isVievwer = false;
    private AvailableRooms? _rooms = null;
    
    private ClientWebSocket? _ws = null;
    
    private DotNetObjectReference<SessionManager>? _dotNetRef;
    private IJSObjectReference? _module;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Manager._dotNetRef = DotNetObjectReference.Create<Menu>(this);
            _dotNetRef = DotNetObjectReference.Create(Manager);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/gamehub.js");
            Manager._module = _module;
        }
    }

    private async Task Connection()
    {
        if (!_connected)
        {
            Console.WriteLine("Connection ...");
            Session.Url = Url;
            await _module!.InvokeVoidAsync("connect", Url, _dotNetRef);
            _connected = true;
            Console.WriteLine("Connected");
        }
    }

    private void SwitchMode()
    {
        _isVievwer = !_isVievwer;
    }

    private async Task Disconnect()
    {
        await _module!.InvokeVoidAsync("disconnect");
        _connected = false;
    }
    
    private async Task CreateRoom()
    {
        if (_rooms == null) return;
        Information content = new();
        if (Session.SessionId != null) content.ID = Session.SessionId;
        else return;
        await _module!.InvokeVoidAsync("sendMessage", new {type = "CreateRoom", room=Session.SubscribedId, payload=content});
    }
    
    
    private async void SelectRoom(string roomId)
    {
        Console.WriteLine($"Room sélectionnée : {roomId}");
        Session.SubscribedId = roomId;
        await _module!.InvokeVoidAsync("sendMessage", new {type = "SwitchRoom", room=roomId, payload=new {}});

        if (_isVievwer)
        {
            Navigation.NavigateTo($"/GameHub/Lobby/{roomId}/Viewer");
        }
        else
        {
            Navigation.NavigateTo($"/GameHub/Room/{roomId}/Player/LR");
        }
    }

    private async Task Reload()
    {
        if (_rooms == null) return;
        string roomId = _rooms.EntryPoint[0];
        Information content = new();
        if (Session.SessionId != null) content.ID = Session.SessionId;
        else return;
        await _module!.InvokeVoidAsync("sendMessage", new {type = "Information:Rooms", room=roomId, payload=content});
    }

    public async Task OnConnected()
    {
        _connected = true;
        Console.WriteLine("Connecté (confirmé par JS) !");
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnError(string message)
    {
        Console.WriteLine($"Erreur WS : {message}");
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnDisconnected(int code, string reason)
    {
        _connected = false;
        Console.WriteLine($"Déconnecté : {code} - {reason}");
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnMessageReceived(string json)
    {
        try
        {
            BaseMessage? msg = JsonSerializer.Deserialize<BaseMessage>(json);
            if (msg == null)
            {
                Console.WriteLine("Message is null");
                return;
            }

            switch (msg.Type)
            {
                case "welcome":
                    Welcome? info = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.ToString());
                        info = msg.Payload.Deserialize<Welcome>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Welcome failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (info != null)
                    {
                        string id = info.playerId;
                        Session.SessionId = id;
                        Console.WriteLine($"Welcome to GameHub, you now are {id}");
                    }

                    break;

                case "Information:Rooms":
                    AvailableRooms? info2 = null;
                    try
                    {
                        info2 = msg.Payload.Deserialize<AvailableRooms>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Information rooms failed");
                        break;
                    }

                    if (info2 != null)
                    {
                        _rooms = info2;
                        Session.SubscribedId = _rooms.EntryPoint[0];
                        Console.WriteLine($"Set the rooms");
                    }

                    break;
                
                case "CreateRoom":
                    CreateRoom info3 = null;
                    try
                    {
                        info3 = msg.Payload.Deserialize<CreateRoom>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Information rooms failed");
                        break;
                    }

                    if (info3 != null)
                    {
                        _rooms.Lobbies.Add(info3.RoomId);
                        Console.WriteLine($"Set the room: {info3.RoomId}");
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

        InvokeAsync(StateHasChanged);
    }
    
    
    public void Dispose()
    {
        if (_ws != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _ws.Dispose();
            _connected = false;
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.InvokeVoidAsync("disconnect");
            await _module.DisposeAsync();
        }
        _dotNetRef?.Dispose();
    }
}