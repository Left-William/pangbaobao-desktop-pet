namespace PangBaoBaoPet;

/// <summary>The transparent window's placement in desktop independent pixels.</summary>
public readonly record struct StageWindow(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    // Keep the character's horizontal center and ground line fixed when the
    // transparent stage grows for a wide action or shrinks back to idle.
    public StageWindow ResizeKeepingCenterAndGround(double width, double height) =>
        new(Left + (Width - width) / 2, Top + Height - height, width, height);

    public StageWindow MoveInto(StageWindow area) =>
        new(System.Math.Clamp(Left, area.Left, System.Math.Max(area.Left, area.Right - Width)),
            System.Math.Clamp(Top, area.Top, System.Math.Max(area.Top, area.Bottom - Height)), Width, Height);

    public bool FitsIn(StageWindow area) =>
        Left >= area.Left && Top >= area.Top && Right <= area.Right && Bottom <= area.Bottom;
}
