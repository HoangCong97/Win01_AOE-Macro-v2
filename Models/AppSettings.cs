namespace AOEKeyboardMacroPro.Models;

public class HudSettings
{
    public bool Enabled { get; set; } = true;
    public int X { get; set; } = 30;
    public int Y { get; set; } = 150;
    public double Opacity { get; set; } = 0.95;
    public bool Locked { get; set; } = false;
}

public class AppSettings
{
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int FarmTimerInterval { get; set; } = 200;
    public ResourceCropSettings ResourceCrop { get; set; } = new();
    public PopCropSettings PopCrop { get; set; } = new();
    public TimerCropSettings TimerCrop { get; set; } = new();
    public ChatCropSettings ChatCrop { get; set; } = new();
    public LoadingCropSettings LoadingCrop { get; set; } = new();
    public UnitQueueCropSettings UnitQueueCrop { get; set; } = new();
    public HudSettings Hud { get; set; } = new();
}
