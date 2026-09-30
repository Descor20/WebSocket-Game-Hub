using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Games;

public partial class MarioGame : BaseGame
{
    // ----- Monde -----
    private const double ViewportWidth = 800;  // zone visible a l'ecran
    private const double LevelWidth = 2600;    // largeur totale du niveau
    private const double WorldHeight = 400;
    private const double GroundHeight = 64;

    // ----- Drapeau de fin de niveau -----
    private const double FlagX = LevelWidth - 120;
    private const double FlagPoleHeight = 180;

    // ----- Mario -----
    private const double MarioWidth = 28;
    private const double MarioHeight = 32;
    private const double Gravity = 1600;       // px/s^2
    private const double JumpVelocity = 560;   // px/s
    private const double MoveSpeed = 220;      // px/s

    // ----- Boucle de jeu -----
    private const int TickMs = 33; // ~30 FPS
    private CancellationTokenSource? _loopCts;

    // ----- Etat -----
    private double _x = 50;
    private double _y = GroundHeight;
    private double _vy = 0;
    private double _cameraX = 0;
    private bool _onGround = true;
    private bool _facingRight = true;
    private bool _isMovingLeft = false;
    private bool _isMovingRight = false;
    private bool _isUsing = false;
    private bool _hasWon = false;

    private record Platform(double X, double Y, double Width);

    // Y = hauteur (depuis le sol) de la surface sur laquelle Mario peut se tenir.
    // Pas encore de texture : ce sont de simples blocs, a remplacer plus tard.
    private readonly List<Platform> _platforms = new()
    {
        new Platform(0, GroundHeight, LevelWidth), // le sol lui-meme
        new Platform(150, 150, 96),
        new Platform(350, 220, 96),
        new Platform(560, 150, 96),
        new Platform(750, 180, 96),
        new Platform(950, 240, 120),
        new Platform(1200, 150, 96),
        new Platform(1450, 200, 150),
        new Platform(1700, 150, 96),
        new Platform(1900, 260, 120),
        new Platform(2150, 180, 96),
        new Platform(2400, 150, 150),
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            StartGameLoop();
        }
    }

    public override async Task OnMessageReceived(string json)
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

                    if (info4 is null)
                    {
                        Console.WriteLine("No command found");
                        break;
                    }

                    HandleControllerAction(info4.action);
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
                        {
                        }
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

    // ----- Actions manette -----
    private void HandleControllerAction(string action)
    {
        // Une fois le drapeau atteint, on ignore les entrees de deplacement
        if (_hasWon && action is "left" or "right" or "up")
        {
            return;
        }

        switch (action)
        {
            case "left":
                _isMovingLeft = true;
                break;
            case "left:up":
                _isMovingLeft = false;
                break;

            case "right":
                _isMovingRight = true;
                break;
            case "right:up":
                _isMovingRight = false;
                break;

            case "up":
                if (_onGround)
                {
                    _vy = JumpVelocity;
                    _onGround = false;
                }
                break;
            case "up:up":
                // Saut "variable" : relacher up tot coupe la montee (comme dans le vrai Mario)
                if (_vy > 0)
                {
                    _vy *= 0.5;
                }
                break;

            case "use":
                TriggerUse();
                break;
            case "use:up":
                // TODO: a implementer plus tard (charge d'une action par exemple)
                break;

            default:
                Console.WriteLine($"Unknown action: {action}");
                break;
        }
    }

    private void TriggerUse()
    {
        _isUsing = true;
        _ = ResetUseFlagAsync();
    }

    private async Task ResetUseFlagAsync()
    {
        await Task.Delay(250);
        _isUsing = false;
        await InvokeAsync(StateHasChanged);
    }

    private void ResetLevel()
    {
        _x = 50;
        _y = GroundHeight;
        _vy = 0;
        _cameraX = 0;
        _onGround = true;
        _facingRight = true;
        _isMovingLeft = false;
        _isMovingRight = false;
        _isUsing = false;
        _hasWon = false;
    }

    // ----- Boucle de jeu / physique -----
    private void StartGameLoop()
    {
        _loopCts = new CancellationTokenSource();
        _ = GameLoopAsync(_loopCts.Token);
    }

    private async Task GameLoopAsync(CancellationToken token)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMs));
        double dt = TickMs / 1000.0;

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                UpdatePhysics(dt);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Boucle arretee proprement lors du Dispose
        }
    }

    private void UpdatePhysics(double dt)
    {
        // --- Horizontal ---
        double vx = _isMovingLeft && !_isMovingRight ? -MoveSpeed
            : _isMovingRight && !_isMovingLeft ? MoveSpeed
            : 0;

        if (vx != 0)
        {
            _facingRight = vx > 0;
        }

        _x += vx * dt;
        _x = Math.Clamp(_x, 0, LevelWidth - MarioWidth);

        // --- Vertical (gravite) ---
        _vy -= Gravity * dt;
        double newY = _y + _vy * dt;

        const double landingTolerance = 6;
        bool landed = false;

        foreach (var platform in _platforms)
        {
            bool overlapsHorizontally = _x + MarioWidth > platform.X && _x < platform.X + platform.Width;
            if (!overlapsHorizontally)
            {
                continue;
            }

            // On ne "atterrit" que si on descend et qu'on etait au niveau (ou au-dessus) de la plateforme
            if (_vy <= 0 && _y >= platform.Y - landingTolerance && newY <= platform.Y)
            {
                newY = platform.Y;
                _vy = 0;
                landed = true;
            }
        }

        _y = Math.Max(newY, 0);
        _onGround = landed;

        // --- Drapeau de fin de niveau ---
        if (!_hasWon && _x + MarioWidth >= FlagX)
        {
            _hasWon = true;
            _x = FlagX;
            _isMovingLeft = false;
            _isMovingRight = false;
        }

        // --- Camera : suit Mario, centree, sans sortir des bords du niveau ---
        _cameraX = Math.Clamp(_x - ViewportWidth / 2 + MarioWidth / 2, 0, Math.Max(0, LevelWidth - ViewportWidth));
    }

    public override void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        base.Dispose();
    }
}