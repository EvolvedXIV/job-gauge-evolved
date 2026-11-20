using System;
using System.Collections.Generic;

namespace JobGaugeEvolved.Configuration;

/// <summary>A comma-separated list of names typed into a setting.</summary>
/// <remarks>Only uses the BCL, so it is unit-tested.</remarks>
internal static class NameList
{
    public static IReadOnlySet<string> Parse(string? text) =>
        new HashSet<string>(
            (text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
}
