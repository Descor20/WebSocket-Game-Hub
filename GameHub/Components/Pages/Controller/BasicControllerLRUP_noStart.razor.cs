using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Controller;

public partial class BasicControllerLRUP_noStart: BaseController, IDisposable, Menu 
{
    public async Task ClickButton(string action, bool isUp)
    {
        if (Session.SessionId is null || Session.SubscribedId is null)
        {
            Console.WriteLine("No available ID");
            return;
        }
        RoomAction content = new RoomAction();
        content.ID = Session.SessionId;
        content.Room = Session.SubscribedId;
        content.Action = action;
        if (isUp)
        {
            content.Action += ":up";
        }

        if (_module is null)
        {
            Console.WriteLine("Module is null");
            throw new Exception("JSmodule is not set");
        }
        else
        {
            await _module.InvokeVoidAsync("sendMessage",
                new { type = "Room:Command", room = Session.SubscribedId, payload = content });
        }
    }
}