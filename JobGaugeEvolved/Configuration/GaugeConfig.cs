using System;
using System.Numerics;

namespace JobGaugeEvolved.Configuration;

public enum PositionMode
{
    /// <summary>Your own position, set with the sliders or by dragging.</summary>
    Free,

    /// <summary>Where the game's own gauge is in the HUD Layout, scaled like it, plus your offset and size.</summary>
    FollowHud,
}

/// <summary>Settings every job gauge has.</summary>
[Serializable]
public abstract class GaugeConfig
{
    public const float MinSize = 0.5f;
    public const float MaxSize = 3f;

    public bool Enabled = true;

    /// <summary>Size multiplier, 0.5 to 3 (50% to 300%).</summary>
    public float Size = 1f;

    public PositionMode PositionMode = PositionMode.Free;

    /// <summary>Centre of the gauge on screen, in pixels. Null until the gauge is first shown or moved.</summary>
    public Vector2? Position;

    /// <summary>Offset from the game gauge's centre in <see cref="PositionMode.FollowHud"/> mode, in pixels.</summary>
    public Vector2 HudOffset;

    /// <summary>Which of the job's game gauges <see cref="PositionMode.FollowHud"/> follows (some jobs have two).</summary>
    public int HudAnchor;

    /// <summary>Hide the game's own gauge for this job while this one is enabled.</summary>
    public bool HideGameGauge = true;

    public bool OnlyInCombat;

    /// <summary>Clamps values that may have been hand-edited or come from an older version.</summary>
    public virtual void Sanitize()
    {
        Size = float.IsFinite(Size) ? Math.Clamp(Size, MinSize, MaxSize) : 1f;
        if (Position is { } position && !(float.IsFinite(position.X) && float.IsFinite(position.Y))) {
            Position = null;
        }

        if (!(float.IsFinite(HudOffset.X) && float.IsFinite(HudOffset.Y))) {
            HudOffset = Vector2.Zero;
        }

        HudAnchor = Math.Max(0, HudAnchor);
        if (!Enum.IsDefined(PositionMode)) {
            PositionMode = PositionMode.Free;
        }
    }
}
