namespace AOEKeyboardMacroPro.Models;

public class AppSettings
{
    public int FarmTimerInterval { get; set; } = 200;
    public ResourceCropSettings ResourceCrop { get; set; } = new();
    public PopCropSettings PopCrop { get; set; } = new();
}
