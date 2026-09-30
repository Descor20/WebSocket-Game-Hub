using System.Reflection.Metadata;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Microsoft.JSInterop;

namespace GameHub.Components.Objects.Scoped;

public class SessionManager
{
    public DotNetObjectReference<Menu>? _dotNetRef { get; set; }
    public IJSObjectReference? _module { get; set; }

    [JSInvokable]
    public Task OnMessageReceived(string msg)
    {
        try
        {
            Console.WriteLine("Receive: " + msg);
            _ = ProcessMessageAsync(msg);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(string msg)
    {
        if (_dotNetRef != null)
        {
            try
            {
                await _dotNetRef.Value.OnMessageReceived(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur dans le handler: " + ex);
            }
        }
        else
        {
            Console.WriteLine("No handler set");
        }
    }

    [JSInvokable]
    public async Task OnConnected()
    {
        if (_dotNetRef != null) _dotNetRef.Value.OnConnected();
        else
        {
            Console.WriteLine("No handler set");
        }
    }

    [JSInvokable]
    public async Task OnError(string message)
    {
        if (_dotNetRef != null) _dotNetRef.Value.OnError(message);
        else
        {
            Console.WriteLine("No handler set");
        }
    }

    [JSInvokable]
    public async Task OnDisconnected(int code, string reason)
    {
        if (_dotNetRef != null) _dotNetRef.Value.OnDisconnected(code, reason);
        else
        {
            Console.WriteLine("No handler set");
        }
    }
}