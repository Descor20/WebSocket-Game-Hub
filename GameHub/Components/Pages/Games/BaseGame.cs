using ConsoleApp1.Objects.Messages;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Games;

public abstract class BaseGame : ComponentBase, IDisposable, Menu
{
    [Parameter]
    public string Id { get; set; } = default!;
    
    [Inject] 
    protected IJSRuntime JS { get; set; } = default!;
    
    [Inject] 
    protected SessionData Session { get; set; }
    
    [Inject] 
    protected SessionManager Manager { get; set; }
    
    protected bool _subscribed { get; set; } = false;
    
    protected IJSObjectReference? _module;
    
    [Inject] 
    protected NavigationManager Navigation { get; set; }
    
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
            content.Role = "Subscriber";
            
            await _module!.InvokeVoidAsync("sendMessage",
                new { type = "Subscribed", room = Session.SubscribedId, payload = content });
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
    }

    public async Task OnDisconnected(int code, string reason)
    {
        Console.WriteLine($"Disconnected : {code} - {reason}");
        Navigation.NavigateTo($"/GameHub/");
    }

    public abstract Task OnMessageReceived(string json);
    
    public virtual void Dispose()
    {
        //TODO: Implement
    }
}