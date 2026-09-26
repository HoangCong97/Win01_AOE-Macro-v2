using System.Windows.Forms;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class GameStateWatcher : IDisposable
{
    private bool _isInGame = false;
    private bool _isInChat = false;

    public event Action<bool>? InGameStatusChanged; // true = in game, false = out of game
    public event Action<bool>? ChatStatusChanged;   // true = in chat, false = out of chat

    public bool IsInGame => _isInGame;
    public bool IsInChat => _isInChat;

    public GameStateWatcher()
    {
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void SetInGameStatus(bool inGame)
    {
        if (_isInGame != inGame)
        {
            _isInGame = inGame;
            InGameStatusChanged?.Invoke(_isInGame);
        }
    }

    public void NotifyEnterKey()
    {
        _isInChat = !_isInChat;
        ChatStatusChanged?.Invoke(_isInChat);
    }

    public void NotifyEscapeKey()
    {
        if (_isInChat)
        {
            _isInChat = false;
            ChatStatusChanged?.Invoke(false);
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
