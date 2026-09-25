namespace AOEKeyboardMacroPro.Models;

public class ResourceCropBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 70;
    public int Height { get; set; } = 20;

    public ResourceCropBox() { }

    public ResourceCropBox(int x, int y, int width = 70, int height = 20)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public ResourceCropBox Clone()
    {
        return new ResourceCropBox(X, Y, Width, Height);
    }
}

public class ResourceCropSettings
{
    public ResourceCropBox Wood { get; set; } = new(73, 12, 70, 20);
    public ResourceCropBox Food { get; set; } = new(174, 12, 70, 20);
    public ResourceCropBox Gold { get; set; } = new(276, 12, 70, 20);
    public ResourceCropBox Stone { get; set; } = new(377, 12, 70, 20);

    public int ZoomLevel { get; set; } = 2; // 1, 2, 3, 4
    public bool TopMost { get; set; } = true;
    public bool AutoRefresh { get; set; } = true;
}

public class PopCropSettings
{
    public ResourceCropBox PopBox { get; set; } = new(672, 27, 52, 12);
    public int Threshold { get; set; } = 255; // Ngưỡng điểm ảnh trắng (R==255 && G==255 && B==255)
}

public class PopValues
{
    public int? CurrentPop { get; set; }
    public int? MaxPop { get; set; }

    public bool IsValid => CurrentPop.HasValue && MaxPop.HasValue;

    public bool EqualsValues(PopValues? other)
    {
        if (other is null) return false;
        return CurrentPop == other.CurrentPop && MaxPop == other.MaxPop;
    }

    public override string ToString()
    {
        if (CurrentPop.HasValue && MaxPop.HasValue) return $"{CurrentPop}/{MaxPop}";
        if (CurrentPop.HasValue) return $"{CurrentPop}/--";
        return "--";
    }
}
