using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Controller;

public partial class CardController: BaseController, IDisposable, Menu 
{
    
    public List<CardBase> Cards { get; set; } = new List<CardBase>();
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
            
        await _module!.InvokeVoidAsync("sendMessage",
            new { type = "Room:Command", room = Session.SubscribedId, payload = content });
    }
}