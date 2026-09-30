namespace ConsoleApp1;

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Server
{
    private const int Port = 8080;
    private bool _isRunning = false;

    private readonly Lobby _lobby;
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    public Server()
    {
        _lobby = new Lobby();
        _listener = new HttpListener();
    }

    public bool Start()
    {
        if (_isRunning) return false;
        _isRunning = true;

        _listener.Prefixes.Add($"http://*:{Port}/");
        //_listener.Prefixes.Add($"http://localhost:{Port}/");
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
                // Listener arrêté (Stop() a été appelé)
                break;
            }

            if (context.Request.IsWebSocketRequest)
            {
                _ = HandleClientAsync(context, _lobby);
            }
            else
            {
                Console.WriteLine("Closing lost connection");
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private static async Task HandleClientAsync(HttpListenerContext context, Lobby lobby)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;
        var player = new Player(Guid.NewGuid().ToString("N")[..8], socket);

        lobby.AddPlayer(player);
        await player.SendAsync("welcome", new { playerId = player.Id });
        await player.SendAsync("lobby:list", new { rooms = lobby.LobbyList() });

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
                await lobby.HandleMessageAsync(player, json);
            }
        }
        catch (WebSocketException)
        {
            Console.WriteLine("Deconnection brutale");
            // Déconnexion brutale du client
        }
        finally
        {
            lobby.RemovePlayer(player);
        }
    }
}

// ------------------------------------------------------------------
// Modèles
// ------------------------------------------------------------------
