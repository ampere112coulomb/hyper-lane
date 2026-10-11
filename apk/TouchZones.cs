using System;

namespace HyperLane.Android;

/// <summary>Reference-image touch regions, scaled from 1920 × 1080 to the current viewport.</summary>
public static class TouchZones
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;
    private const float Top = 616f;
    private const float Bottom = 1064f;
    private const float OuterMargin = 16f;
    private const float Gap = 32f;

    public static bool Supports(int lanes) => lanes is 3 or 4 or 5 or 6 or 8 or 12;

    public static TouchRect GetRect(int lanes, int lane, float width, float height, int heightPercent = 50)
    {
        if (!Supports(lanes)) throw new ArgumentOutOfRangeException(nameof(lanes));
        if ((uint)lane >= (uint)lanes) throw new ArgumentOutOfRangeException(nameof(lane));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

        float zoneWidth = (ReferenceWidth - OuterMargin * 2f - Gap * (lanes - 1)) / lanes;
        float left = OuterMargin + lane * (zoneWidth + Gap);
        float percent = Math.Clamp(heightPercent, 0, 100);
        float zoneHeight = percent <= 50 ? (Bottom - Top) * percent / 50 : Bottom - Top + Top * (percent - 50) / 50;
        return new TouchRect(left * width / ReferenceWidth,
            (Bottom - zoneHeight) * height / ReferenceHeight,
            (left + zoneWidth) * width / ReferenceWidth,
            Bottom * height / ReferenceHeight);
    }

    public static int HitTest(int lanes, float x, float y, float width, float height, int heightPercent = 50)
    {
        if (!Supports(lanes) || width <= 0 || height <= 0) return -1;
        for (int lane = 0; lane < lanes; lane++)
        {
            if (GetRect(lanes, lane, width, height, heightPercent).Contains(x, y)) return lane;
        }
        return -1;
    }
}

public readonly record struct TouchRect(float Left, float Top, float Right, float Bottom)
{
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
