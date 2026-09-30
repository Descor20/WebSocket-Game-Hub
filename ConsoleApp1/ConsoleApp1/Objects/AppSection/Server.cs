using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Rooms;
using ConsoleApp1.Objects.User;

namespace ConsoleApp1.Objects.AppSection;

public class Server
{
    private const int Port = 8080;
    private bool _isRunning = false;
    
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    
    public Server(int nbLobby)
    {
        EntryPage _ = new EntryPage(this);
        
        _listener = new HttpListener();
    }
    
    public bool Start()
    {
        if (_isRunning) return false;
        _isRunning = true;

        _listener.Prefixes.Add($"http://*:{Port}/");
        _listener.Start();

        Console.WriteLine($"Serveur WebSocket de lobby en écoute sur le port {Port}");

        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));

        return true;
    }
    
    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _cts?.Cancel();
        _listener.Stop();
        _listener.Close();
    }
    
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (token.IsCancellationRequested || !_listener.IsListening)
            {
                Console.WriteLine("Something1");
                // Listener arrêté (Stop() a été appelé)
                break;
            }

            if (context.Request.IsWebSocketRequest)
            {
                _ = HandleClientAsync(context);
            }
            else
            {
                Console.WriteLine("Closing lost connection");
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }
    
    private static async Task HandleClientAsync(HttpListenerContext context)
    {
        Console.Write("New client");
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;
        var player = new Player(Guid.NewGuid().ToString("N")[..8], socket);
        
        Console.WriteLine($"Client connected: {player.Id}");
        IdManager.AddUser(player);
        
        IdManager.EntryPage?.AddPlayer(player);
        await player.SendAsync("welcome", new { playerId = player.Id });
        await player.SendAsync("Information:Rooms", IdManager.GetAvailableRooms());

        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fermeture", CancellationToken.None);
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                try
                {
                    BaseMessage? msg = JsonSerializer.Deserialize<BaseMessage>(json);
                    if (msg == null)
                    {
                        Console.WriteLine("Message is not a BaseMessage");
                        throw new Exception();
                    }
                    Listener? listener = IdManager.GetListener(msg.RoomId);
                    if (listener == null)
                    {
                        Console.WriteLine("No listener Found");
                        Console.WriteLine(msg.RoomId);
                        Console.WriteLine(IdManager.GetAvailableRooms());
                        throw new Exception();
                    }

                    if (msg.Type == "SwitchRoom")
                    {
                        string? room = IdManager.GetRegistration(player.Id);
                        if (!(room is null))
                        {
                            Listener? oldListener = IdManager.GetListener(room);
                            if (!(oldListener is null))
                            {
                                oldListener.RemovePlayer(player);
                            }
                        }
                        
                        listener.AddPlayer(player);
                    }
                    else
                    {
                        await listener.HandleMessageAsync(player, json);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await player.SendAsync("error", new { message = "invalid JSON" });
                    continue;
                }
            }
        }
        catch (WebSocketException)
        {
            Console.WriteLine("Brutal deconnection");
        }
        finally
        {
            IdManager.EntryPage?.RemovePlayer(player);
            IdManager.RemoveUser(player);
        }
    }
}