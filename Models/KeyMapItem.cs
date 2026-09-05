namespace AOEKeyboardMacroPro.Models;

public class KeyMapItem
{
    public string PhysicalKey { get; set; } = string.Empty;
    public string MappedKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Column { get; set; }

    public KeyMapItem(string physicalKey, string mappedKey, string description, int row, int col)
    {
        PhysicalKey = physicalKey;
        MappedKey = mappedKey;
        Description = description;
        Row = row;
        Column = col;
    }
}
