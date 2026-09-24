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
var affection = new AffectionService(new PetState { Affection = 29 });
var reward = affection.Register(PetInteraction.Click, start);
Check(reward.Points == 1 && reward.NewMilestones.SequenceEqual(new[] { "kiss" }), "Kiss threshold did not trigger once");
Check(affection.Register(PetInteraction.Click, start.AddSeconds(1)).Points == 0, "Click cooldown ignored");
Check(affection.NextPending() == "kiss", "First reward was not queued");
affection.Complete("kiss");
Check(affection.NextPending() is null, "Completed reward remained pending");
affection.State.Affection = 69;
var rollReward = affection.Register(PetInteraction.Drag, start.AddSeconds(61));
Check(rollReward.NewMilestones.SequenceEqual(new[] { "roll" }), "Roll threshold did not trigger once");
Check(affection.Register(PetInteraction.Drag, start.AddSeconds(62)).Points == 0, "Drag cooldown ignored");
Check(affection.NextPending() == "roll", "Roll reward was not queued");
var recoveredUnlock = new PetState { Affection = 30 };
recoveredUnlock.Normalize();
Check(recoveredUnlock.PendingMilestones.SequenceEqual(new[] { "kiss" }), "Threshold lost between scoring and queueing");
var cap = new AffectionService(new PetState());
for (var i = 0; i < 10; i++) cap.Register(PetInteraction.Click, start.AddSeconds(i * 4));
Check(cap.State.Affection == 6, "Rolling score cap ignored");
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
    File.WriteAllText(SettingsStore.FilePath,
        "{\"Version\":1,\"Lines\":[{\"Enabled\":true,\"Text\":\"旧台词\"}],\"ProbabilityPercent\":42}");
    var migrated = SettingsStore.Load(out var migrationWarning);
    Check(migrationWarning is null && migrated.Version == 2 && migrated.ProbabilityPercent == 42 &&
          migrated.Lines.Any(x => x.Text == "旧台词" && x.Context == "ambient") &&
          migrated.Lines.Any(x => x.Context == "click"), "Old dialogue settings were not migrated");
    PetStateStore.Save(affection.State);
    var persisted = PetStateStore.Load(out var stateWarning);
    Check(stateWarning is null && persisted.Affection == affection.State.Affection &&
          persisted.PendingMilestones.SequenceEqual(new[] { "roll" }) &&
          persisted.CompletedMilestones.SequenceEqual(new[] { "kiss" }), "Affection state did not persist");
    persisted.Affection = 72;
    PetStateStore.Save(persisted);
    File.WriteAllText(PetStateStore.FilePath, "{ invalid json");
    var restoredState = PetStateStore.Load(out var restoredWarning);
    Check(restoredWarning is not null && restoredState.Affection == affection.State.Affection,
        "Corrupt affection state did not recover");
}
finally
{
    var full = Path.GetFullPath(testDirectory);
    if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        Directory.Delete(full, recursive: true);
    Environment.SetEnvironmentVariable("PANGBAOBAO_CONFIG_DIR", null);
}
Console.WriteLine("PASS: bubble rules, affection thresholds and cooldowns, persistence backup recovery");
