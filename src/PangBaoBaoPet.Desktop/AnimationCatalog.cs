using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PangBaoBaoPet;

public sealed class AnimationAction
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string SkinId { get; init; } = "pajamas";
    public string Category { get; init; } = "routine";
    public IReadOnlyList<string> FramePaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<double> FrameEnds { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> FrameDisplayHeights { get; init; } = Array.Empty<double>();
    public IReadOnlyList<(double X, double Y)> FrameOffsets { get; init; } = Array.Empty<(double X, double Y)>();
    public IReadOnlyList<(double X, double Y)> FrameHeadAnchors { get; init; } = Array.Empty<(double X, double Y)>();
    public IReadOnlyList<(double X, double Y)> FrameMouthAnchors { get; init; } = Array.Empty<(double X, double Y)>();
    public double StageWidth { get; init; } = 430;
    public double StageHeight { get; init; } = 490;
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
        public string SkinId { get; set; } = "pajamas";
        public string Category { get; set; } = "routine";
        public string? AssetDirectory { get; set; }
        public int? Frames { get; set; }
        public int? Fps { get; set; }
        public int[]? DurationsMs { get; set; }
        public double[]? FrameDisplayHeights { get; set; }
        public double[][]? FrameOffsets { get; set; }
        public double[][]? FrameHeadAnchors { get; set; }
        public double[][]? FrameMouthAnchors { get; set; }
        public double[]? StageDip { get; set; }
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
            if (spec.FrameOffsets is { } offsets &&
                (offsets.Length != paths.Length || offsets.Any(x => x is not { Length: 2 } ||
                    x.Any(value => !double.IsFinite(value) || Math.Abs(value) > 300))))
                throw new InvalidDataException($"Invalid frame offsets: {spec.Id}");
            if (spec.FrameHeadAnchors is { } heads && !ValidAnchors(heads, paths.Length) ||
                spec.FrameMouthAnchors is { } mouths && !ValidAnchors(mouths, paths.Length))
                throw new InvalidDataException($"Invalid frame anchors: {spec.Id}");
            if (spec.StageDip is { } stage && (stage.Length != 2 ||
                !double.IsFinite(stage[0]) || !double.IsFinite(stage[1]) ||
                stage[0] is < 430 or > 1024 || stage[1] is < 490 or > 768))
                throw new InvalidDataException($"Invalid stage size: {spec.Id}");
            if (!double.IsFinite(spec.DisplayHeight) || spec.DisplayHeight <= 0 ||
                !double.IsFinite(spec.HeadX) || spec.HeadX is < 0 or > 1 ||
                !double.IsFinite(spec.HeadY) || spec.HeadY is < 0 or > 1)
                throw new InvalidDataException($"Invalid action geometry: {spec.Id}");
            double total = 0;
            var ends = new List<double>();
            for (var i = 0; i < paths.Length; i++)
            {
                total += spec.DurationsMs is { Length: > 0 }
                    ? spec.DurationsMs[i] / 1000.0
                    : 1.0 / Math.Max(1, spec.Fps ?? 8);
                ends.Add(total);
            }
            result.Add(new AnimationAction { Id = spec.Id, Name = spec.Name,
                SkinId = spec.SkinId, Category = spec.Category, FramePaths = paths,
                FrameEnds = ends, DisplayHeight = spec.DisplayHeight,
                StageWidth = spec.StageDip?[0] ?? 430,
                StageHeight = spec.StageDip?[1] ?? 490,
                FrameDisplayHeights = spec.FrameDisplayHeights ?? Enumerable.Repeat(spec.DisplayHeight, paths.Length).ToArray(),
                FrameOffsets = spec.FrameOffsets is { } frameOffsets
                    ? frameOffsets.Select(x => (x[0], x[1])).ToArray()
                    : Enumerable.Repeat((0.0, 0.0), paths.Length).ToArray(),
                FrameHeadAnchors = spec.FrameHeadAnchors is { } headAnchors
                    ? headAnchors.Select(x => (x[0], x[1])).ToArray()
                    : Enumerable.Repeat((spec.HeadX, spec.HeadY), paths.Length).ToArray(),
                FrameMouthAnchors = spec.FrameMouthAnchors is { } mouthAnchors
                    ? mouthAnchors.Select(x => (x[0], x[1])).ToArray()
                    : Enumerable.Repeat((spec.HeadX, Math.Min(1, spec.HeadY + 0.05)), paths.Length).ToArray(),
                IncludeInRoutine = spec.IncludeInRoutine, OneShot = spec.OneShot,
                HeadX = spec.HeadX, HeadY = spec.HeadY });
        }
        return result;
    }

    private static bool ValidAnchors(double[][] anchors, int count) =>
        anchors.Length == count && anchors.All(anchor => anchor is { Length: 2 } &&
            anchor.All(value => double.IsFinite(value) && value is >= 0 and <= 1));
}
