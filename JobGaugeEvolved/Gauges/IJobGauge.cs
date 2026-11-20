using System;
using System.Collections.Generic;
using System.Numerics;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Gauges;

/// <summary>Role, for grouping gauges in the settings window.</summary>
internal enum JobRole
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    MagicalRanged,
}

/// <summary>One job's gauge.</summary>
internal interface IJobGauge : IDisposable
{
    uint JobId { get; }
    string Name { get; }
    JobRole Role { get; }
    GaugeConfig Config { get; }

    /// <summary>The art (layout and images) the gauge draws with.</summary>
    ArtAtlas Art { get; }

    /// <summary>Everything the gauge can draw, in gauge units; the overlay window covers this.</summary>
    ArtBox Bounds { get; }

    /// <summary>The point that is placed at the gauge's position, in gauge units.</summary>
    Vector2 Centre { get; }

    /// <summary>The game's own HUD addons for this job's gauge (hidden when the option is on).</summary>
    IReadOnlyList<string> GameAddons { get; }

    /// <summary>The title and help text of the settings for <see cref="GaugeConfig.Glow"/>, saying what it glows around.</summary>
    (string Title, string Help) GlowSettingsText { get; }

    /// <summary>What players call each of <see cref="GameAddons"/>, in the same order.</summary>
    IReadOnlyList<string> GameGaugeNames { get; }

    /// <summary>Names of the made-up states the settings window can preview.</summary>
    IReadOnlyList<string> PreviewStates { get; }

    /// <summary>Whether <see cref="Update"/> found something to show.</summary>
    bool HasData { get; }

    /// <summary>
    /// Reads the game (framework thread). With <paramref name="previewState"/> set, shows that made-up state instead.
    /// </summary>
    void Update(int? previewState);

    /// <summary>Draws the gauge (ImGui, every frame while visible).</summary>
    void Draw(GaugeCanvas canvas, GaugeText text, double time);

    /// <summary>The job-specific part of the settings. Returns whether anything changed.</summary>
    bool DrawSettings();

    /// <summary>Live values for the debug window.</summary>
    void DrawDebug();
}
