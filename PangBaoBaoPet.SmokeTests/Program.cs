using PangBaoBaoPet;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var start = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.FromHours(8));
var settings = new AppSettings
{
    CheckIntervalSeconds = 5,
    CooldownSeconds = 10,
    ProbabilityPercent = 0,
    Lines = new() { new() { Text = "第一句" }, new() { Text = "第二句" } }
};
var rng = new Random(42);
var scheduler = new BubbleScheduler(start, settings);
Check(scheduler.Tick(start.AddSeconds(4), settings, rng, true) is null, "Early bubble");
Check(scheduler.Tick(start.AddSeconds(5), settings, rng, true) is null, "0% triggered");
settings.ProbabilityPercent = 100;
var first = scheduler.Tick(start.AddSeconds(10), settings, rng, true);
Check(first is not null, "100% did not trigger");
scheduler.Reset(start.AddSeconds(11), settings);
Check(scheduler.IsShowing, "Reset lost an active bubble");
Check(scheduler.Tick(start.AddSeconds(15), settings, rng, true) is null, "Multiple simultaneous bubbles");
scheduler.Dismiss(start.AddSeconds(16), settings);
Check(scheduler.Tick(start.AddSeconds(20), settings, rng, true) is null, "Cooldown ignored");
var second = scheduler.Tick(start.AddSeconds(26), settings, rng, true);
Check(second is not null && second != first, "No-repeat choice failed");
scheduler.Dismiss(start.AddSeconds(27), settings);
settings.BubbleEnabled = false;
Check(scheduler.Tick(start.AddSeconds(40), settings, rng, true) is null, "Disabled bubble triggered");
settings.BubbleEnabled = true;
settings.Lines.Clear();
Check(scheduler.Tick(start.AddSeconds(45), settings, rng, true) is null, "Empty library triggered");
settings.Lines.Add(new DialogueLine { Text = "恢复" });
Check(scheduler.Tick(start.AddSeconds(50), settings, rng, false) is null, "Hidden pet triggered");
Check(scheduler.Tick(start.AddSeconds(55), settings, rng, true) == "恢复", "Visible pet did not trigger");
Check(scheduler.Tick(start.AddSeconds(55), settings, rng, true) is null, "Catch-up bubble triggered");
settings.ProbabilityPercent = -12;
settings.Scale = double.PositiveInfinity;
settings.Normalize();
Check(settings.ProbabilityPercent == 0 && settings.Scale == 1, "Invalid settings not normalized");
var testDirectory = Path.Combine(Path.GetTempPath(), "PangBaoBaoPetSmoke-" + Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("PANGBAOBAO_CONFIG_DIR", testDirectory);
try
{
    settings.ProbabilityPercent = 20;
    SettingsStore.Save(settings);
    settings.ProbabilityPercent = 37;
    SettingsStore.Save(settings);
    Check(File.Exists(SettingsStore.BackupPath), "Backup not written");
    File.WriteAllText(SettingsStore.FilePath, "{ invalid json");
    var recovered = SettingsStore.Load(out var warning);
    Check(recovered.ProbabilityPercent == 20 && warning is not null, "Corrupt settings did not recover from backup");
    var reread = SettingsStore.Load(out var secondWarning);
    Check(reread.ProbabilityPercent == 20 && secondWarning is null, "Recovered primary was not restored");
}
finally
{
    var full = Path.GetFullPath(testDirectory);
    if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        Directory.Delete(full, recursive: true);
    Environment.SetEnvironmentVariable("PANGBAOBAO_CONFIG_DIR", null);
}
Console.WriteLine("PASS: bubble rules, input normalization, atomic settings backup recovery");
