using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PangBaoBaoPet;

public sealed class DialogueLine
{
    public bool Enabled { get; set; } = true;
    public string Text { get; set; } = "";
    public string Context { get; set; } = "ambient";
}

public sealed class AppSettings
{
    public int Version { get; set; } = 2;
    public bool BubbleEnabled { get; set; } = true;
    public bool AffectionEnabled { get; set; } = true;
    public int InteractionBubbleProbabilityPercent { get; set; } = 25;
    public int CheckIntervalSeconds { get; set; } = 300;
    public int ProbabilityPercent { get; set; } = 20;
    public int CooldownSeconds { get; set; } = 600;
    public int BubbleSeconds { get; set; } = 5;
    public List<DialogueLine> Lines { get; set; } = new()
    {
        new() { Text = "做操呢，别盯着我看。" },
        new() { Text = "你也起来活动两下。" },
        new() { Text = "先别吵，正在扩胸。" },
        new() { Text = "摸我头干嘛……我还在做操呢。", Context = "click" },
        new() { Text = "放这儿是吧？行，我接着练。", Context = "drag" },
        new() { Text = "给你一个，别耽误我做操。", Context = "kiss" },
        new() { Text = "看完了？扶我起来继续。", Context = "roll" }
    };
    public double Scale { get; set; } = 1.0;
    public double Speed { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public bool AutoRoutine { get; set; } = true;
    public string ActionId { get; set; } = "stretch";
    public string SkinId { get; set; } = "pajamas";
    public double? Left { get; set; }
    public double? Top { get; set; }

    public void Normalize()
    {
        CheckIntervalSeconds = Math.Clamp(CheckIntervalSeconds, 5, 3600);
        ProbabilityPercent = Math.Clamp(ProbabilityPercent, 0, 100);
        InteractionBubbleProbabilityPercent = Math.Clamp(InteractionBubbleProbabilityPercent, 0, 100);
        CooldownSeconds = Math.Clamp(CooldownSeconds, 0, 86400);
        BubbleSeconds = Math.Clamp(BubbleSeconds, 1, 30);
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, 0.5, 2.0) : 1.0;
        Speed = double.IsFinite(Speed) ? Math.Clamp(Speed, 0.25, 3.0) : 1.0;
        Lines ??= new();
        if (Version < 2)
        {
            Lines.AddRange(new[]
            {
                new DialogueLine { Text = "摸我头干嘛……我还在做操呢。", Context = "click" },
                new DialogueLine { Text = "放这儿是吧？行，我接着练。", Context = "drag" },
                new DialogueLine { Text = "给你一个，别耽误我做操。", Context = "kiss" },
                new DialogueLine { Text = "看完了？扶我起来继续。", Context = "roll" }
            });
            Version = 2;
        }
        Lines = Lines.Where(line => line is not null).Select(line => new DialogueLine
        {
            Enabled = line.Enabled,
            Text = (line.Text ?? "").Trim(),
            Context = line.Context is "click" or "drag" or "kiss" or "roll" ? line.Context : "ambient"
        }).Take(200).ToList();
        ActionId ??= "stretch";
        if (SkinId is not ("pajamas" or "black-tee")) SkinId = "pajamas";
        if (Left is double left && !double.IsFinite(left)) Left = null;
        if (Top is double top && !double.IsFinite(top)) Top = null;
    }
}

public static class SettingsStore
{
    public static string DirectoryPath =>
        Environment.GetEnvironmentVariable("PANGBAOBAO_CONFIG_DIR") is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PangBaoBaoPet");
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public static string BackupPath => FilePath + ".bak";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath)) return new AppSettings();
        try { return Read(FilePath); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            try
            {
                var backup = Read(BackupPath);
                try { File.Copy(BackupPath, FilePath, true); }
                catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException) { }
                warning = "设置文件损坏，已从备份恢复。";
                return backup;
            }
            catch (Exception backupError) when (backupError is IOException or JsonException or UnauthorizedAccessException)
            {
                warning = "设置文件损坏，已使用默认设置；原文件未删除。";
                return new AppSettings();
            }
        }
    }

    private static AppSettings Read(string path)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? throw new JsonException("Empty settings");
        settings.Normalize();
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(DirectoryPath);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
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
