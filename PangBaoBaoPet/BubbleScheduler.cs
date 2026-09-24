using System;
using System.Linq;

namespace PangBaoBaoPet;

public sealed class BubbleScheduler
{
    private DateTimeOffset _nextCheck;
    private DateTimeOffset _cooldownUntil;
    private int _lastLine = -1;
    public bool IsShowing { get; private set; }

    public BubbleScheduler(DateTimeOffset now, AppSettings settings) => Reset(now, settings);

    public void Reset(DateTimeOffset now, AppSettings settings)
    {
        _nextCheck = now.AddSeconds(settings.CheckIntervalSeconds);
    }

    public string? Tick(DateTimeOffset now, AppSettings settings, Random random, bool visible)
    {
        if (now < _nextCheck) return null;
        _nextCheck = now.AddSeconds(settings.CheckIntervalSeconds);
        if (!settings.BubbleEnabled || !visible || IsShowing || now < _cooldownUntil) return null;
        var choices = settings.Lines.Select((line, index) => (line, index))
            .Where(x => x.line.Enabled && !string.IsNullOrWhiteSpace(x.line.Text)).ToArray();
        if (choices.Length == 0 || random.Next(100) >= settings.ProbabilityPercent) return null;
        var candidates = choices.Length > 1 ? choices.Where(x => x.index != _lastLine).ToArray() : choices;
        var selected = candidates[random.Next(candidates.Length)];
        _lastLine = selected.index;
        IsShowing = true;
        return selected.line.Text;
    }

    public void Preview() => IsShowing = true;

    public void Dismiss(DateTimeOffset now, AppSettings settings)
    {
        if (!IsShowing) return;
        IsShowing = false;
        _cooldownUntil = now.AddSeconds(settings.CooldownSeconds);
    }
}
