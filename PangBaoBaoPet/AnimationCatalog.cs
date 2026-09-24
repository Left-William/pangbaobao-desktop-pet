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
    public IReadOnlyList<double> FrameDisplayHeights { get; init; } = Array.Empty<double>();
    public double DisplayHeight { get; init; } = 362;
    public bool IncludeInRoutine { get; init; } = true;
    public bool OneShot { get; init; }
    public double HeadX { get; init; } = 0.5;
    public double HeadY { get; init; } = 0.15;
    public double Duration => FrameEnds[^1];

    public int FrameAt(double seconds)
    {
        var offset = OneShot ? Math.Min(seconds, Duration - 0.000001) : seconds % Duration;
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
        public string? AssetDirectory { get; set; }
        public int? Frames { get; set; }
        public int? Fps { get; set; }
        public int[]? DurationsMs { get; set; }
        public double[]? FrameDisplayHeights { get; set; }
        public double DisplayHeight { get; set; } = 362;
        public bool IncludeInRoutine { get; set; } = true;
        public bool OneShot { get; set; }
        public double HeadX { get; set; } = 0.5;
        public double HeadY { get; set; } = 0.15;
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
            if (string.IsNullOrWhiteSpace(spec.Id) || spec.Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                spec.Id.Contains("..", StringComparison.Ordinal) ||
                spec.AssetDirectory is { } directory &&
                (directory.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || directory.Contains("..", StringComparison.Ordinal)))
                throw new InvalidDataException($"Invalid action id or asset directory: {spec.Id}");
            var paths = Directory.GetFiles(Path.Combine(root, spec.AssetDirectory ?? spec.Id), "frame_*.png").OrderBy(x => x).ToArray();
            if (paths.Length == 0) throw new InvalidDataException($"No frames: {spec.Id}");
            if (spec.Frames is { } declared && declared != paths.Length)
                throw new InvalidDataException($"Frame count mismatch: {spec.Id}");
            if (spec.DurationsMs is { } durations &&
                (durations.Length != paths.Length || durations.Any(x => x <= 0)))
                throw new InvalidDataException($"Invalid frame durations: {spec.Id}");
            if (spec.FrameDisplayHeights is { } heights &&
                (heights.Length != paths.Length || heights.Any(x => !double.IsFinite(x) || x <= 0)))
                throw new InvalidDataException($"Invalid frame heights: {spec.Id}");
            if (!double.IsFinite(spec.DisplayHeight) || spec.DisplayHeight <= 0 ||
                !double.IsFinite(spec.HeadX) || spec.HeadX is < 0 or > 1 ||
                !double.IsFinite(spec.HeadY) || spec.HeadY is < 0 or > 1)
                throw new InvalidDataException($"Invalid action geometry: {spec.Id}");
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
                    ? spec.DurationsMs[i] / 1000.0
                    : 1.0 / Math.Max(1, spec.Fps ?? 8);
                ends.Add(total);
            }
            result.Add(new AnimationAction { Id = spec.Id, Name = spec.Name, Frames = frames,
                FrameEnds = ends, DisplayHeight = spec.DisplayHeight,
                FrameDisplayHeights = spec.FrameDisplayHeights ?? Enumerable.Repeat(spec.DisplayHeight, frames.Length).ToArray(),
                IncludeInRoutine = spec.IncludeInRoutine, OneShot = spec.OneShot,
                HeadX = spec.HeadX, HeadY = spec.HeadY });
        }
        return result;
    }
}
