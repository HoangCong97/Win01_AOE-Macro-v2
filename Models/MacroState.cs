namespace AOEKeyboardMacroPro.Models;

public enum MacroState
{
    Disabled,            // Tắt
    Active,              // Hoạt động
    SuspendedChat,       // Tạm dừng (Chat)
    SuspendedOutOfGame   // Tạm dừng (Ngoài game)
}

public static class MacroStateExtensions
{
    public static string ToDisplayName(this MacroState state)
    {
        return state switch
        {
            MacroState.Disabled => "Tắt",
            MacroState.Active => "Hoạt động",
            MacroState.SuspendedChat => "Tạm dừng (Chat)",
            MacroState.SuspendedOutOfGame => "Tạm dừng (Ngoài game)",
            _ => "Không xác định"
        };
    }
}
