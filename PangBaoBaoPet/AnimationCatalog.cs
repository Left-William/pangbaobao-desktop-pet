using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace PangBaoBaoPet;

public sealed class AnimationAction
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<BitmapImage> Frames { get; init; } = Array.Empty<BitmapImage>();
    public IReadOnlyList<double> FrameEnds { get; init; } = Array.Empty<double>();
    public double DisplayHeight { get; init; } = 362;
    public bool IncludeInRoutine { get; init; } = true;
    public double Duration => FrameEnds[^1];

    public int FrameAt(double seconds)
    {
        var offset = seconds % Duration;
        for (var i = 0; i < FrameEnds.Count; i++) if (offset < FrameEnds[i]) return i;
        return FrameEnds.Count - 1;
    }
}

public static class AnimationCatalog
{
    private sealed class Spec
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int? Fps { get; set; }
        public int[]? DurationsMs { get; set; }
        public double DisplayHeight { get; set; } = 362;
        public bool IncludeInRoutine { get; set; } = true;
    }

    public static IReadOnlyList<AnimationAction> Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");
        var specs = JsonSerializer.Deserialize<List<Spec>>(
            File.ReadAllText(Path.Combine(root, "actions.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("No actions");
        var result = new List<AnimationAction>();
        foreach (var spec in specs)
        {
            var paths = Directory.GetFiles(Path.Combine(root, spec.Id), "frame_*.png").OrderBy(x => x).ToArray();
            if (paths.Length == 0) throw new InvalidDataException($"No frames: {spec.Id}");
            var frames = paths.Select(path =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }).ToArray();
            double total = 0;
            var ends = new List<double>();
            for (var i = 0; i < frames.Length; i++)
            {
                total += spec.DurationsMs is { Length: > 0 }
                    ? spec.DurationsMs[Math.Min(i, spec.DurationsMs.Length - 1)] / 1000.0
                    : 1.0 / Math.Max(1, spec.Fps ?? 8);
                ends.Add(total);
            }
            result.Add(new AnimationAction { Id = spec.Id, Name = spec.Name, Frames = frames,
                FrameEnds = ends, DisplayHeight = spec.DisplayHeight,
                IncludeInRoutine = spec.IncludeInRoutine });
        }
        return result;
    }
}
