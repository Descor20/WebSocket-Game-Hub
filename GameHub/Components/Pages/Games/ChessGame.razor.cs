using System.Text.Json;
using ConsoleApp1.Objects.Messages;
using ConsoleApp1.Objects.Messages.Response;
using ConsoleApp1.Objects.Utils;
using GameHub.Components.Objects;
using GameHub.Components.Objects.Scoped;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GameHub.Components.Pages.Games;

public enum PieceType
{
    None = 0,
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

public enum PieceColor
{
    None = 0,
    White,
    Black
}

public readonly struct Piece
{
    public PieceType Type { get; }
    public PieceColor Color { get; }

    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }

    public bool IsEmpty => Type == PieceType.None;
}

public partial class ChessGame : BaseGame
{
    // ------------------------------------------------------------------
    //  Board / game state
    // ------------------------------------------------------------------
    private Piece[,] _board = CreateInitialBoard();
    private PieceColor _turn = PieceColor.White;
    private bool _inCheck;
    private bool _gameOver;
    private string _statusText = "";

    private (int Row, int Col)? _selected;
    private List<(int Row, int Col)> _legalTargets = new();
    private (int Row, int Col)? _lastMoveFrom;
    private (int Row, int Col)? _lastMoveTo;
    private (int Row, int Col)? _enPassantTarget;
    private (int FromRow, int FromCol, int ToRow, int ToCol)? _pendingPromotion;

    private bool _whiteKingMoved;
    private bool _blackKingMoved;
    private bool _whiteRookAMoved;
    private bool _whiteRookHMoved;
    private bool _blackRookAMoved;
    private bool _blackRookHMoved;
    private int _halfmoveClock;

    private readonly List<PieceType> _capturedByWhite = new();
    private readonly List<PieceType> _capturedByBlack = new();
    private readonly List<string> _moveHistory = new();

    // ------------------------------------------------------------------
    //  Player / networking state
    // ------------------------------------------------------------------
    private readonly HashSet<string> _knownPlayers = new();
    private string? _whiteId;
    private string? _blackId;

    private bool ColorsAssigned => _whiteId != null && _blackId != null;

    private PieceColor MyColor
    {
        get
        {
            if (Session.SessionId is null) return PieceColor.None;
            if (Session.SessionId == _whiteId) return PieceColor.White;
            if (Session.SessionId == _blackId) return PieceColor.Black;
            return PieceColor.None;
        }
    }

    private bool IsMyTurn => _subscribed && ColorsAssigned && !_gameOver && MyColor == _turn;

    private IEnumerable<int> DisplayRows =>
        MyColor == PieceColor.Black ? Enumerable.Range(0, 8).Reverse() : Enumerable.Range(0, 8);

    private IEnumerable<int> DisplayCols =>
        MyColor == PieceColor.Black ? Enumerable.Range(0, 8).Reverse() : Enumerable.Range(0, 8);

    // ------------------------------------------------------------------
    //  Static move data
    // ------------------------------------------------------------------
    private static readonly (int, int)[] BishopDirs = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private static readonly (int, int)[] RookDirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    private static readonly (int, int)[] QueenDirs =
    {
        (1, 1), (1, -1), (-1, 1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1)
    };
    private static readonly (int, int)[] KnightOffsets =
    {
        (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)
    };

    // ==================================================================
    //  Networking / message handling
    // ==================================================================
    override
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

                        if (Session.SessionId is not null)
                        {
                            _knownPlayers.Add(Session.SessionId);
                            UpdateColorAssignment();
                        }

                        await SendRoomCommand("join");
                    }
                    else
                    {
                        Console.WriteLine("Subscription failed");
                    }
                    break;

                case "Room:Command":
                    RoomAction? action = null;
                    try
                    {
                        Console.WriteLine(msg.Payload.ToString());
                        action = msg.Payload.Deserialize<RoomAction>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("command room failed");
                        Console.WriteLine(ex.Message);
                        break;
                    }

                    if (action is null)
                    {
                        Console.WriteLine("No command found");
                        break;
                    }

                    HandleRoomAction(action);
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
                        catch (Exception)
                        {
                        }
                    }
                    break;
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Something unexpected happened");
        }

        await InvokeAsync(StateHasChanged);
    }

    private void HandleRoomAction(RoomAction action)
    {
        if (string.IsNullOrEmpty(action.Action))
        {
            Console.WriteLine("Empty action received");
            return;
        }

        if (action.Action == "join")
        {
            if (!string.IsNullOrEmpty(action.ID))
            {
                _knownPlayers.Add(action.ID);
            }
            UpdateColorAssignment();
        }
        else if (action.Action == "restart")
        {
            ResetGame();
        }
        else if (action.Action.StartsWith("move:"))
        {
            HandleIncomingMove(action);
        }
        else
        {
            Console.WriteLine($"Unknown chess action: {action.Action}");
        }
    }

    private void UpdateColorAssignment()
    {
        if (ColorsAssigned) return;
        if (_knownPlayers.Count < 2) return;

        List<string> ordered = _knownPlayers.OrderBy(x => x, StringComparer.Ordinal).Take(2).ToList();
        _whiteId = ordered[0];
        _blackId = ordered[1];
        EvaluateGameState();
    }

    private void HandleIncomingMove(RoomAction action)
    {
        if (!ColorsAssigned || _gameOver) return;

        // Ignore our own move being echoed back to us: we already applied it optimistically.
        if (action.ID == Session.SessionId) return;

        string expectedId = _turn == PieceColor.White ? _whiteId! : _blackId!;
        if (action.ID != expectedId)
        {
            Console.WriteLine("Move ignored: unexpected sender for the current turn");
            return;
        }

        string payload = action.Action.Substring("move:".Length);
        if (payload.Length < 4) return;

        (int fr, int fc) = ParseSquare(payload.Substring(0, 2));
        (int tr, int tc) = ParseSquare(payload.Substring(2, 2));
        if (!InBounds(fr, fc) || !InBounds(tr, tc)) return;
        if (_board[fr, fc].IsEmpty) return;

        PieceType promo = payload.Length > 4 ? ParsePromotion(payload[4]) : PieceType.Queen;
        CommitMove(fr, fc, tr, tc, promo);
    }

    private async Task SendRoomCommand(string action)
    {
        if (Session.SessionId is null || Session.SubscribedId is null || _module is null) return;

        RoomAction content = new RoomAction
        {
            ID = Session.SessionId,
            Room = Session.SubscribedId,
            Action = action
        };

        await _module.InvokeVoidAsync("sendMessage",
            new { type = "Room:Command", room = Session.SubscribedId, payload = content });
    }

    private async Task SendMove(int fr, int fc, int tr, int tc, PieceType? promotion)
    {
        string action = $"move:{SquareName(fr, fc)}{SquareName(tr, tc)}";
        if (promotion.HasValue) action += PromotionChar(promotion.Value);
        await SendRoomCommand(action);
    }

    private async Task RequestRestart()
    {
        ResetGame();
        await SendRoomCommand("restart");
    }

    private void LeaveGame()
    {
        Navigation.NavigateTo("/GameHub/");
    }

    // ==================================================================
    //  UI interaction
    // ==================================================================
    public async Task OnSquareClicked(int r, int c)
    {
        if (!IsMyTurn) return;
        if (_pendingPromotion is not null) return;

        Piece piece = _board[r, c];

        if (_selected is { } sel)
        {
            if (_legalTargets.Any(t => t.Row == r && t.Col == c))
            {
                Piece movingPiece = _board[sel.Row, sel.Col];
                bool needsPromotion = movingPiece.Type == PieceType.Pawn && (r == 0 || r == 7);

                _selected = null;
                _legalTargets.Clear();

                if (needsPromotion)
                {
                    _pendingPromotion = (sel.Row, sel.Col, r, c);
                    return;
                }

                int fr = sel.Row, fc = sel.Col;
                CommitMove(fr, fc, r, c, PieceType.Queen);
                await SendMove(fr, fc, r, c, null);
                return;
            }

            if (piece.Color == MyColor)
            {
                _selected = (r, c);
                _legalTargets = GetLegalMoves(r, c);
                return;
            }

            _selected = null;
            _legalTargets.Clear();
            return;
        }

        if (piece.Color == MyColor)
        {
            _selected = (r, c);
            _legalTargets = GetLegalMoves(r, c);
        }
    }

    public async Task ChoosePromotion(PieceType type)
    {
        if (_pendingPromotion is not { } p) return;
        _pendingPromotion = null;

        CommitMove(p.FromRow, p.FromCol, p.ToRow, p.ToCol, type);
        await SendMove(p.FromRow, p.FromCol, p.ToRow, p.ToCol, type);
        await InvokeAsync(StateHasChanged);
    }

    private bool IsSelected(int r, int c) => _selected is { } s && s.Row == r && s.Col == c;

    private bool IsLegalTarget(int r, int c) => _legalTargets.Any(t => t.Row == r && t.Col == c);

    private bool IsLastMove(int r, int c) =>
        (_lastMoveFrom is { } f && f.Row == r && f.Col == c) ||
        (_lastMoveTo is { } t && t.Row == r && t.Col == c);

    private bool IsKingInCheckSquare(int r, int c) =>
        _inCheck && _board[r, c].Type == PieceType.King && _board[r, c].Color == _turn;

    private bool IsBottomEdge(int r) => MyColor == PieceColor.Black ? r == 0 : r == 7;

    private bool IsLeftEdge(int c) => MyColor == PieceColor.Black ? c == 7 : c == 0;

    private static string SquareColorClass(int r, int c) => (r + c) % 2 == 0 ? "light" : "dark";

    // ==================================================================
    //  Game lifecycle
    // ==================================================================
    private void ResetGame()
    {
        _board = CreateInitialBoard();
        _turn = PieceColor.White;
        _inCheck = false;
        _gameOver = false;
        _selected = null;
        _legalTargets.Clear();
        _lastMoveFrom = null;
        _lastMoveTo = null;
        _enPassantTarget = null;
        _pendingPromotion = null;

        _whiteKingMoved = _blackKingMoved = false;
        _whiteRookAMoved = _whiteRookHMoved = false;
        _blackRookAMoved = _blackRookHMoved = false;
        _halfmoveClock = 0;

        _capturedByWhite.Clear();
        _capturedByBlack.Clear();
        _moveHistory.Clear();

        EvaluateGameState();
    }

    private void CommitMove(int fr, int fc, int tr, int tc, PieceType promotion)
    {
        Piece moving = _board[fr, fc];
        Piece captured = _board[tr, tc];

        bool isPawn = moving.Type == PieceType.Pawn;
        bool isEnPassant = isPawn && fc != tc && _board[tr, tc].IsEmpty;
        bool isDoublePush = isPawn && Math.Abs(tr - fr) == 2;
        bool isCastle = moving.Type == PieceType.King && Math.Abs(tc - fc) == 2;
        bool isPromotion = isPawn && (tr == 0 || tr == 7);

        PieceType capturedType = PieceType.None;
        if (isEnPassant) capturedType = PieceType.Pawn;
        else if (!captured.IsEmpty) capturedType = captured.Type;

        ApplyMoveToBoard(_board, fr, fc, tr, tc, promotion);

        _enPassantTarget = isDoublePush ? ((fr + tr) / 2, fc) : null;

        if (moving.Type == PieceType.King)
        {
            if (moving.Color == PieceColor.White) _whiteKingMoved = true;
            else _blackKingMoved = true;
        }
        if (moving.Type == PieceType.Rook)
        {
            if (fr == 7 && fc == 0) _whiteRookAMoved = true;
            else if (fr == 7 && fc == 7) _whiteRookHMoved = true;
            else if (fr == 0 && fc == 0) _blackRookAMoved = true;
            else if (fr == 0 && fc == 7) _blackRookHMoved = true;
        }
        if (tr == 7 && tc == 0) _whiteRookAMoved = true;
        if (tr == 7 && tc == 7) _whiteRookHMoved = true;
        if (tr == 0 && tc == 0) _blackRookAMoved = true;
        if (tr == 0 && tc == 7) _blackRookHMoved = true;

        _halfmoveClock = (isPawn || capturedType != PieceType.None) ? 0 : _halfmoveClock + 1;

        if (capturedType != PieceType.None)
        {
            if (moving.Color == PieceColor.White) _capturedByWhite.Add(capturedType);
            else _capturedByBlack.Add(capturedType);
        }

        string moveText;
        if (isCastle)
        {
            moveText = tc > fc ? "O-O" : "O-O-O";
        }
        else
        {
            string letter = PieceLetter(moving.Type);
            string sep = capturedType != PieceType.None ? "x" : "-";
            moveText = $"{letter}{SquareName(fr, fc)}{sep}{SquareName(tr, tc)}";
            if (isPromotion) moveText += $"={PieceLetter(promotion)}";
        }
        _moveHistory.Add(moveText);

        _lastMoveFrom = (fr, fc);
        _lastMoveTo = (tr, tc);

        _turn = Opposite(_turn);
        _selected = null;
        _legalTargets.Clear();

        EvaluateGameState();
    }

    private void EvaluateGameState()
    {
        if (!ColorsAssigned)
        {
            _statusText = "En attente d'un second joueur...";
            return;
        }

        _inCheck = IsKingInCheck(_turn);
        bool hasMoves = HasAnyLegalMove(_turn);

        if (!hasMoves)
        {
            _gameOver = true;
            if (_inCheck)
            {
                PieceColor winner = Opposite(_turn);
                _statusText = $"Échec et mat ! Les {ColorLabel(winner)} gagnent.";
            }
            else
            {
                _statusText = "Pat ! Partie nulle.";
            }
        }
        else if (_halfmoveClock >= 100)
        {
            _gameOver = true;
            _statusText = "Partie nulle (règle des 50 coups).";
        }
        else if (IsInsufficientMaterial())
        {
            _gameOver = true;
            _statusText = "Partie nulle (matériel insuffisant).";
        }
        else
        {
            _gameOver = false;
            _statusText = _inCheck
                ? $"Échec ! Trait aux {ColorLabel(_turn)}."
                : $"Trait aux {ColorLabel(_turn)}.";
        }
    }

    // ==================================================================
    //  Move generation / legality
    // ==================================================================
    private List<(int Row, int Col)> GetLegalMoves(int r, int c)
    {
        Piece piece = _board[r, c];
        List<(int Row, int Col)> result = new();
        if (piece.IsEmpty) return result;

        foreach ((int tr, int tc) in GetPseudoMoves(r, c))
        {
            if (!WouldLeaveKingInCheck(r, c, tr, tc, piece.Color))
            {
                result.Add((tr, tc));
            }
        }
        return result;
    }

    private bool HasAnyLegalMove(PieceColor color)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (_board[i, j].Color == color && GetLegalMoves(i, j).Count > 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool WouldLeaveKingInCheck(int fr, int fc, int tr, int tc, PieceColor color)
    {
        Piece[,] clone = CloneBoard(_board);
        ApplyMoveToBoard(clone, fr, fc, tr, tc, PieceType.Queen);
        (int Row, int Col) kingPos = FindKing(clone, color);
        return IsSquareAttacked(clone, kingPos.Row, kingPos.Col, Opposite(color));
    }

    private bool IsKingInCheck(PieceColor color)
    {
        (int Row, int Col) kingPos = FindKing(_board, color);
        return IsSquareAttacked(_board, kingPos.Row, kingPos.Col, Opposite(color));
    }

    private List<(int, int)> GetPseudoMoves(int r, int c)
    {
        Piece piece = _board[r, c];
        List<(int, int)> moves = new();
        if (piece.IsEmpty) return moves;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(r, c, piece.Color, moves);
                break;
            case PieceType.Knight:
                AddKnightMoves(r, c, piece.Color, moves);
                break;
            case PieceType.Bishop:
                AddSlidingMoves(r, c, piece.Color, BishopDirs, moves);
                break;
            case PieceType.Rook:
                AddSlidingMoves(r, c, piece.Color, RookDirs, moves);
                break;
            case PieceType.Queen:
                AddSlidingMoves(r, c, piece.Color, QueenDirs, moves);
                break;
            case PieceType.King:
                AddKingMoves(r, c, piece.Color, moves);
                break;
        }
        return moves;
    }

    private void AddPawnMoves(int r, int c, PieceColor color, List<(int, int)> moves)
    {
        int dir = color == PieceColor.White ? -1 : 1;
        int startRow = color == PieceColor.White ? 6 : 1;

        int nr = r + dir;
        if (InBounds(nr, c) && _board[nr, c].IsEmpty)
        {
            moves.Add((nr, c));
            int nr2 = r + 2 * dir;
            if (r == startRow && _board[nr2, c].IsEmpty)
            {
                moves.Add((nr2, c));
            }
        }

        foreach (int dc in new[] { -1, 1 })
        {
            int nc = c + dc;
            if (!InBounds(nr, nc)) continue;

            if (!_board[nr, nc].IsEmpty && _board[nr, nc].Color != color)
            {
                moves.Add((nr, nc));
            }
            else if (_board[nr, nc].IsEmpty && _enPassantTarget is { } ep && ep.Row == nr && ep.Col == nc)
            {
                moves.Add((nr, nc));
            }
        }
    }

    private void AddKnightMoves(int r, int c, PieceColor color, List<(int, int)> moves)
    {
        foreach ((int dr, int dc) in KnightOffsets)
        {
            int nr = r + dr, nc = c + dc;
            if (!InBounds(nr, nc)) continue;
            if (_board[nr, nc].IsEmpty || _board[nr, nc].Color != color)
            {
                moves.Add((nr, nc));
            }
        }
    }

    private void AddSlidingMoves(int r, int c, PieceColor color, (int, int)[] dirs, List<(int, int)> moves)
    {
        foreach ((int dr, int dc) in dirs)
        {
            int nr = r + dr, nc = c + dc;
            while (InBounds(nr, nc))
            {
                if (_board[nr, nc].IsEmpty)
                {
                    moves.Add((nr, nc));
                }
                else
                {
                    if (_board[nr, nc].Color != color) moves.Add((nr, nc));
                    break;
                }
                nr += dr;
                nc += dc;
            }
        }
    }

    private void AddKingMoves(int r, int c, PieceColor color, List<(int, int)> moves)
    {
        foreach ((int dr, int dc) in QueenDirs)
        {
            int nr = r + dr, nc = c + dc;
            if (!InBounds(nr, nc)) continue;
            if (_board[nr, nc].IsEmpty || _board[nr, nc].Color != color)
            {
                moves.Add((nr, nc));
            }
        }

        bool inCheckNow = IsSquareAttacked(_board, r, c, Opposite(color));
        if (inCheckNow) return;

        bool kingMoved = color == PieceColor.White ? _whiteKingMoved : _blackKingMoved;
        if (kingMoved) return;

        bool rookHMoved = color == PieceColor.White ? _whiteRookHMoved : _blackRookHMoved;
        bool rookAMoved = color == PieceColor.White ? _whiteRookAMoved : _blackRookAMoved;
        int row = r;

        if (!rookHMoved && _board[row, 5].IsEmpty && _board[row, 6].IsEmpty
            && !IsSquareAttacked(_board, row, 5, Opposite(color))
            && !IsSquareAttacked(_board, row, 6, Opposite(color)))
        {
            moves.Add((row, 6));
        }

        if (!rookAMoved && _board[row, 1].IsEmpty && _board[row, 2].IsEmpty && _board[row, 3].IsEmpty
            && !IsSquareAttacked(_board, row, 3, Opposite(color))
            && !IsSquareAttacked(_board, row, 2, Opposite(color)))
        {
            moves.Add((row, 2));
        }
    }

    private static bool IsSquareAttacked(Piece[,] board, int row, int col, PieceColor byColor)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                Piece p = board[i, j];
                if (p.IsEmpty || p.Color != byColor) continue;

                switch (p.Type)
                {
                    case PieceType.Pawn:
                        int attackRow = i + (byColor == PieceColor.White ? -1 : 1);
                        if (attackRow == row && (j - 1 == col || j + 1 == col)) return true;
                        break;
                    case PieceType.Knight:
                        foreach ((int dr, int dc) in KnightOffsets)
                        {
                            if (i + dr == row && j + dc == col) return true;
                        }
                        break;
                    case PieceType.Bishop:
                        if (SlidingAttacks(board, i, j, row, col, BishopDirs)) return true;
                        break;
                    case PieceType.Rook:
                        if (SlidingAttacks(board, i, j, row, col, RookDirs)) return true;
                        break;
                    case PieceType.Queen:
                        if (SlidingAttacks(board, i, j, row, col, QueenDirs)) return true;
                        break;
                    case PieceType.King:
                        if (Math.Max(Math.Abs(i - row), Math.Abs(j - col)) == 1) return true;
                        break;
                }
            }
        }
        return false;
    }

    private static bool SlidingAttacks(Piece[,] board, int i, int j, int row, int col, (int, int)[] dirs)
    {
        foreach ((int dr, int dc) in dirs)
        {
            int nr = i + dr, nc = j + dc;
            while (InBounds(nr, nc))
            {
                if (nr == row && nc == col) return true;
                if (!board[nr, nc].IsEmpty) break;
                nr += dr;
                nc += dc;
            }
        }
        return false;
    }

    private bool IsInsufficientMaterial()
    {
        List<PieceType> nonKing = new();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                Piece p = _board[i, j];
                if (!p.IsEmpty && p.Type != PieceType.King) nonKing.Add(p.Type);
            }
        }

        if (nonKing.Count == 0) return true;
        if (nonKing.Count == 1 && (nonKing[0] == PieceType.Knight || nonKing[0] == PieceType.Bishop)) return true;
        return false;
    }

    private static (int Row, int Col) FindKing(Piece[,] board, PieceColor color)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (board[i, j].Type == PieceType.King && board[i, j].Color == color)
                {
                    return (i, j);
                }
            }
        }
        return (-1, -1);
    }

    private static Piece[,] CloneBoard(Piece[,] src)
    {
        Piece[,] clone = new Piece[8, 8];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                clone[i, j] = src[i, j];
            }
        }
        return clone;
    }

    private static void ApplyMoveToBoard(Piece[,] board, int fr, int fc, int tr, int tc, PieceType promotion)
    {
        Piece moving = board[fr, fc];
        bool isPawn = moving.Type == PieceType.Pawn;
        bool isEnPassant = isPawn && fc != tc && board[tr, tc].IsEmpty;
        bool isCastle = moving.Type == PieceType.King && Math.Abs(tc - fc) == 2;

        board[tr, tc] = moving;
        board[fr, fc] = default;

        if (isEnPassant)
        {
            board[fr, tc] = default;
        }

        if (isPawn && (tr == 0 || tr == 7))
        {
            board[tr, tc] = new Piece(promotion, moving.Color);
        }

        if (isCastle)
        {
            int row = fr;
            if (tc > fc)
            {
                board[row, 5] = board[row, 7];
                board[row, 7] = default;
            }
            else
            {
                board[row, 3] = board[row, 0];
                board[row, 0] = default;
            }
        }
    }

    private static Piece[,] CreateInitialBoard()
    {
        Piece[,] b = new Piece[8, 8];
        PieceType[] backRank =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int c = 0; c < 8; c++)
        {
            b[0, c] = new Piece(backRank[c], PieceColor.Black);
            b[1, c] = new Piece(PieceType.Pawn, PieceColor.Black);
            b[6, c] = new Piece(PieceType.Pawn, PieceColor.White);
            b[7, c] = new Piece(backRank[c], PieceColor.White);
        }
        return b;
    }

    // ==================================================================
    //  Small helpers
    // ==================================================================
    private static bool InBounds(int r, int c) => r is >= 0 and < 8 && c is >= 0 and < 8;

    private static PieceColor Opposite(PieceColor c) => c == PieceColor.White ? PieceColor.Black : PieceColor.White;

    private static string SquareName(int r, int c) => $"{(char)('a' + c)}{8 - r}";

    private static (int Row, int Col) ParseSquare(string s)
    {
        int col = s[0] - 'a';
        int rank = s[1] - '0';
        int row = 8 - rank;
        return (row, col);
    }

    private static char PromotionChar(PieceType t) => t switch
    {
        PieceType.Queen => 'q',
        PieceType.Rook => 'r',
        PieceType.Bishop => 'b',
        PieceType.Knight => 'n',
        _ => 'q'
    };

    private static PieceType ParsePromotion(char c) => c switch
    {
        'q' => PieceType.Queen,
        'r' => PieceType.Rook,
        'b' => PieceType.Bishop,
        'n' => PieceType.Knight,
        _ => PieceType.Queen
    };

    private static string PieceLetter(PieceType t) => t switch
    {
        PieceType.King => "R",
        PieceType.Queen => "D",
        PieceType.Rook => "T",
        PieceType.Bishop => "F",
        PieceType.Knight => "C",
        _ => ""
    };

    private static string ColorLabel(PieceColor color) => color == PieceColor.White ? "Blancs" : "Noirs";

    private static string PieceSymbolFor(PieceType type, PieceColor color) => (type, color) switch
    {
        (PieceType.King, PieceColor.White) => "\u2654",
        (PieceType.Queen, PieceColor.White) => "\u2655",
        (PieceType.Rook, PieceColor.White) => "\u2656",
        (PieceType.Bishop, PieceColor.White) => "\u2657",
        (PieceType.Knight, PieceColor.White) => "\u2658",
        (PieceType.Pawn, PieceColor.White) => "\u2659",
        (PieceType.King, PieceColor.Black) => "\u265A",
        (PieceType.Queen, PieceColor.Black) => "\u265B",
        (PieceType.Rook, PieceColor.Black) => "\u265C",
        (PieceType.Bishop, PieceColor.Black) => "\u265D",
        (PieceType.Knight, PieceColor.Black) => "\u265E",
        (PieceType.Pawn, PieceColor.Black) => "\u265F",
        _ => ""
    };

    private static string PieceSymbol(Piece p) => p.IsEmpty ? "" : PieceSymbolFor(p.Type, p.Color);
}