using System;
using System.Numerics;

namespace JobGaugeEvolved.Configuration;

public enum ColorMode
{
    Single,
    Gradient,
}

public enum GradientDirection
{
    TopToBottom,
    LeftToRight,
}

/// <summary>
/// A colour setting: one colour, or a gradient from <see cref="A"/> to <see cref="B"/> across the shape.
/// Gauge art is white or grey where it can be recoloured, so this colour multiplies the art.
/// </summary>
/// <remarks>Only uses the BCL, so it is compiled into the tests as well.</remarks>
[Serializable]
public sealed class ColorSpec
{
    public ColorMode Mode = ColorMode.Single;
    public Vector4 A = Vector4.One;
    public Vector4 B = Vector4.One;
    public GradientDirection Direction = GradientDirection.TopToBottom;

    public ColorSpec()
    {
    }

    public ColorSpec(Vector4 color)
    {
        A = color;
        B = color;
    }

    public ColorSpec(Vector4 from, Vector4 to, GradientDirection direction = GradientDirection.TopToBottom)
    {
        Mode = ColorMode.Gradient;
        A = from;
        B = to;
        Direction = direction;
    }

    /// <summary>The colour at position <paramref name="t"/> (0 = start, 1 = end) along the gradient.</summary>
    public Vector4 At(float t) => Mode == ColorMode.Single ? A : Vector4.Lerp(A, B, Math.Clamp(t, 0f, 1f));

    /// <summary>
    /// Corner colours (top left, top right, bottom right, bottom left) for a quad that covers the part of the shape
    /// from <paramref name="x0"/> to <paramref name="x1"/> horizontally and <paramref name="y0"/> to
    /// <paramref name="y1"/> vertically, in fractions of the whole shape. A cropped quad (a partly filled bar) thus
    /// shows its slice of the full gradient rather than the whole gradient squeezed. For a <paramref name="mirrored"/>
    /// shape a left-to-right gradient runs right to left, so it goes the same way along both halves of a symmetric
    /// gauge (tip to hilt on both blades).
    /// </summary>
    public (Vector4 TopLeft, Vector4 TopRight, Vector4 BottomRight, Vector4 BottomLeft) Corners(
        float x0 = 0f, float x1 = 1f, float y0 = 0f, float y1 = 1f, bool mirrored = false)
    {
        if (Mode == ColorMode.Single) {
            return (A, A, A, A);
        }

        if (Direction == GradientDirection.TopToBottom) {
            return (At(y0), At(y0), At(y1), At(y1));
        }

        return mirrored ? (At(x1), At(x0), At(x0), At(x1)) : (At(x0), At(x1), At(x1), At(x0));
    }

    /// <summary>
    /// The colour at a point of a shape, given as fractions (0 to 1) across its width and down its height. On a
    /// <paramref name="mirrored"/> shape a left-to-right gradient runs right to left (tip to hilt on both blades).
    /// </summary>
    public Vector4 AtPosition(float acrossWidth, float downHeight, bool mirrored = false) =>
        Direction == GradientDirection.TopToBottom
            ? At(downHeight)
            : At(mirrored ? 1f - acrossWidth : acrossWidth);

    public ColorSpec Clone() => new() { Mode = Mode, A = A, B = B, Direction = Direction };

    public void CopyFrom(ColorSpec other)
    {
        Mode = other.Mode;
        A = other.A;
        B = other.B;
        Direction = other.Direction;
    }

    public void Sanitize()
    {
        A = Vector4.Clamp(A, Vector4.Zero, Vector4.One);
        B = Vector4.Clamp(B, Vector4.Zero, Vector4.One);
        if (!Enum.IsDefined(Mode)) {
            Mode = ColorMode.Single;
        }

        if (!Enum.IsDefined(Direction)) {
            Direction = GradientDirection.TopToBottom;
        }
    }

    /// <summary>Packs a colour the way ImGui expects it (ABGR, red in the low byte), scaling alpha.</summary>
    public static uint Pack(Vector4 color, float alpha = 1f)
    {
        static uint Channel(float value) => (uint)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);

        return Channel(color.X) | (Channel(color.Y) << 8) | (Channel(color.Z) << 16) | (Channel(color.W * alpha) << 24);
    }

    /// <summary>A colour from a hex string such as "#5BD15B" (for defaults).</summary>
    public static Vector4 Hex(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return new Vector4(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1f);
    }
}
