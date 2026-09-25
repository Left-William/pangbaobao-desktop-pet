using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PangBaoBaoPet;

public enum PetInteraction { Click, Drag }

public sealed class PetState
{
    public int Version { get; set; } = 1;
    public int Affection { get; set; }
    public List<string> PendingMilestones { get; set; } = new();
    public List<string> CompletedMilestones { get; set; } = new();
    public DateTimeOffset? LastClickAt { get; set; }
    public DateTimeOffset? LastDragAt { get; set; }
    public DateTimeOffset? ScoreWindowStartedAt { get; set; }
    public int ScoreWindowPoints { get; set; }
    public DateTimeOffset? LastSpecialAt { get; set; }

    public void Normalize()
    {
        Affection = Math.Clamp(Affection, 0, 100);
        ScoreWindowPoints = Math.Clamp(ScoreWindowPoints, 0, 6);
        PendingMilestones ??= new();
        CompletedMilestones ??= new();
        PendingMilestones = PendingMilestones.Where(IsMilestone).Distinct().ToList();
        CompletedMilestones = CompletedMilestones.Where(IsMilestone).Distinct().ToList();
        PendingMilestones.RemoveAll(CompletedMilestones.Contains);
        // A crash between scoring and queuing must not permanently lose an unlock.
        foreach (var (id, threshold) in Milestones)
            if (Affection >= threshold && !CompletedMilestones.Contains(id) && !PendingMilestones.Contains(id))
                PendingMilestones.Add(id);
        PendingMilestones = PendingMilestones.OrderBy(id => id == "kiss" ? 0 : 1).ToList();
    }

    public static readonly (string Id, int Threshold)[] Milestones = { ("kiss", 30), ("roll", 70) };
    public static bool IsMilestone(string id) => id is "kiss" or "roll";
}

public readonly record struct AffectionResult(int Points, IReadOnlyList<string> NewMilestones);

public sealed class AffectionService
{
    public PetState State { get; }

    public AffectionService(PetState state)
    {
        State = state;
        State.Normalize();
    }

    public AffectionResult Register(PetInteraction kind, DateTimeOffset now)
    {
        var last = kind == PetInteraction.Click ? State.LastClickAt : State.LastDragAt;
        var cooldown = kind == PetInteraction.Click ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(10);
        if (last is { } previous && now >= previous && now - previous < cooldown)
            return new AffectionResult(0, Array.Empty<string>());
        if (State.ScoreWindowStartedAt is not { } start || now < start || now - start >= TimeSpan.FromSeconds(60))
        {
            State.ScoreWindowStartedAt = now;
            State.ScoreWindowPoints = 0;
        }
        var requested = kind == PetInteraction.Click ? 1 : 2;
        var points = Math.Min(requested, Math.Min(6 - State.ScoreWindowPoints, 100 - State.Affection));
        if (points <= 0) return new AffectionResult(0, Array.Empty<string>());
        var before = State.Affection;
        State.Affection += points;
        State.ScoreWindowPoints += points;
        if (kind == PetInteraction.Click) State.LastClickAt = now;
        else State.LastDragAt = now;
        var newMilestones = new List<string>();
        foreach (var (id, threshold) in PetState.Milestones)
            if (before < threshold && State.Affection >= threshold &&
                !State.CompletedMilestones.Contains(id) && !State.PendingMilestones.Contains(id))
            {
                State.PendingMilestones.Add(id);
                newMilestones.Add(id);
            }
        return new AffectionResult(points, newMilestones);
    }

    public string? NextPending() => State.PendingMilestones.FirstOrDefault();

    public void Complete(string id)
    {
        State.PendingMilestones.Remove(id);
        if (!State.CompletedMilestones.Contains(id)) State.CompletedMilestones.Add(id);
    }

    public bool CanPlayOccasional(DateTimeOffset now) =>
        State.CompletedMilestones.Count > 0 &&
        (State.LastSpecialAt is not { } last || now < last || now - last >= TimeSpan.FromSeconds(120));

    public void RecordSpecial(DateTimeOffset now) => State.LastSpecialAt = now;
}

public static class PetStateStore
{
    public static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "pet-state.json");
    public static string BackupPath => FilePath + ".bak";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static PetState Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath)) return new PetState();
        try { return Read(FilePath); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                var backup = Read(BackupPath);
                try { File.Copy(BackupPath, FilePath, true); }
                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException) { }
                warning = "好感度存档损坏，已从备份恢复。";
                return backup;
            }
            catch (Exception backupError) when (backupError is IOException or JsonException or UnauthorizedAccessException)
            {
                warning = "好感度存档损坏，已从零开始；原文件未删除。";
                return new PetState();
            }
        }
    }

    private static PetState Read(string path)
    {
        var state = JsonSerializer.Deserialize<PetState>(File.ReadAllText(path), Options)
            ?? throw new JsonException("Empty pet state");
        state.Normalize();
        return state;
    }

    public static void Save(PetState state)
    {
        state.Normalize();
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(state, Options));
        if (File.Exists(FilePath))
        {
            try { File.Replace(tempPath, FilePath, BackupPath); }
            catch (PlatformNotSupportedException)
            {
                File.Copy(FilePath, BackupPath, true);
                File.Move(tempPath, FilePath, true);
            }
        }
        else File.Move(tempPath, FilePath);
    }
}
