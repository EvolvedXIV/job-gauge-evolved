using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;

namespace JobGaugeEvolved.Rendering;

/// <summary>
/// One gauge's art: its layout and its layer images, embedded in the plugin under <c>Assets/&lt;folder&gt;/</c>.
/// </summary>
internal sealed class ArtAtlas
{
    private static readonly Assembly Assembly = typeof(ArtAtlas).Assembly;

    // Resource names with '/' separators (the build may use '\' on Windows) -> the real resource name.
    private static readonly Lazy<Dictionary<string, string>> ResourceNames = new(() =>
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Assembly.GetManifestResourceNames()) {
            names[name.Replace('\\', '/')] = name;
        }

        return names;
    });

    private readonly string folder;

    public ArtAtlas(string folder)
    {
        this.folder = folder;
        using var stream = Open("layout.json");
        using var reader = new StreamReader(stream);
        Layout = ArtLayout.Parse(reader.ReadToEnd());

        foreach (var layer in Layout.Layers.Keys) {
            foreach (var scale in Layout.Scales) {
                if (!ResourceNames.Value.ContainsKey(ResourcePath(layer, scale))) {
                    throw new FileNotFoundException($"Gauge art {ResourcePath(layer, scale)} is not embedded");
                }
            }
        }
    }

    public ArtLayout Layout { get; }

    /// <summary>Outlines traced from the layers' transparency, for glows and colour fills.</summary>
    public ShapeLibrary Shapes => field ??= new ShapeLibrary(this);

    /// <summary>The texture of a layer at a resolution, or false while it is still loading.</summary>
    public bool TryGetTexture(string layer, int scale, out IDalamudTextureWrap texture)
    {
        var shared = Service.TextureProvider.GetFromManifestResource(Assembly, ResourceNames.Value[ResourcePath(layer, scale)]);
        return shared.TryGetWrap(out texture!, out _);
    }

    private string ResourcePath(string layer, int scale) => $"Assets/{folder}/{layer}@{scale}x.png";

    private Stream Open(string file)
    {
        var path = $"Assets/{folder}/{file}";
        if (!ResourceNames.Value.TryGetValue(path, out var name) || Assembly.GetManifestResourceStream(name) is not { } stream) {
            throw new FileNotFoundException($"Gauge art {path} is not embedded");
        }

        return stream;
    }
}
