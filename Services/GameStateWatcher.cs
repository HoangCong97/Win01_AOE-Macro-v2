using System.Text;
using System.Windows.Forms;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class GameStateWatcher : IDisposable
{
    private readonly System.Windows.Forms.Timer _checkTimer = new();
    private bool _isInGame = false;
    private bool _isInChat = false;

    public event Action<bool>? InGameStatusChanged; // true = in game, false = out of game
    public event Action<bool>? ChatStatusChanged;   // true = in chat, false = out of chat

    public bool IsInGame => _isInGame;
    public bool IsInChat => _isInChat;

    public GameStateWatcher()
    {
        _checkTimer.Interval = 300; // Check active window title every 300ms
        _checkTimer.Tick += CheckActiveWindow;
    }

    public void Start()
    {
        _checkTimer.Start();
    }

    public void Stop()
    {
        _checkTimer.Stop();
    }

    private void CheckActiveWindow(object? sender, EventArgs e)
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        StringBuilder sb = new(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString();

        // Check if window is AOE / Age of Empires / Empire / DE / Application title
        bool newlyInGame = IsAOEGameWindow(title);

        if (newlyInGame != _isInGame)
        {
            _isInGame = newlyInGame;
            InGameStatusChanged?.Invoke(_isInGame);
        }
    }

    private static bool IsAOEGameWindow(string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle)) return false;

        string titleLower = windowTitle.ToLowerInvariant();
        return titleLower.Contains("empire") ||
               titleLower.Contains("age of empires") ||
               titleLower.Contains("aoe") ||
               titleLower.Contains("definitive edition");
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
        _checkTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
