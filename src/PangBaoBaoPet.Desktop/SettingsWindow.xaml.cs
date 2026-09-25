using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PangBaoBaoPet;

public partial class SettingsWindow : Window
{
    private sealed record DialogueContextChoice(string Id, string Label);
    private readonly AppSettings _source;
    private readonly ObservableCollection<DialogueLine> _lines;
    public AppSettings? ResultSettings { get; private set; }

    public SettingsWindow(AppSettings settings, int affection)
    {
        InitializeComponent();
        _source = settings;
        _lines = new ObservableCollection<DialogueLine>(settings.Lines.Select(x => new DialogueLine { Enabled = x.Enabled, Text = x.Text, Context = x.Context }));
        LinesGrid.ItemsSource = _lines;
        AffectionStatusText.Text = $"当前好感度 {affection}/100  ·  飞吻 30  ·  打滚 70";
        ContextColumn.ItemsSource = new[]
        {
            new DialogueContextChoice("ambient", "日常"),
            new DialogueContextChoice("click", "摸头"),
            new DialogueContextChoice("drag", "拖动"),
            new DialogueContextChoice("kiss", "飞吻"),
            new DialogueContextChoice("roll", "打滚")
        };
        BubbleEnabledBox.IsChecked = settings.BubbleEnabled;
        AffectionEnabledBox.IsChecked = settings.AffectionEnabled;
        InteractionProbabilityBox.Text = settings.InteractionBubbleProbabilityPercent.ToString();
        IntervalBox.Text = settings.CheckIntervalSeconds.ToString();
        ProbabilityBox.Text = settings.ProbabilityPercent.ToString();
        CooldownBox.Text = settings.CooldownSeconds.ToString();
        DurationBox.Text = settings.BubbleSeconds.ToString();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var line = new DialogueLine();
        _lines.Add(line);
        LinesGrid.SelectedItem = line;
        LinesGrid.ScrollIntoView(line);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (LinesGrid.SelectedItem is DialogueLine line) _lines.Remove(line);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!ReadNumber(IntervalBox, 5, 3600, out var interval) ||
            !ReadNumber(ProbabilityBox, 0, 100, out var probability) ||
            !ReadNumber(InteractionProbabilityBox, 0, 100, out var interactionProbability) ||
            !ReadNumber(CooldownBox, 0, 86400, out var cooldown) ||
            !ReadNumber(DurationBox, 1, 30, out var duration)) return;
        ResultSettings = new AppSettings
        {
            BubbleEnabled = BubbleEnabledBox.IsChecked == true,
            AffectionEnabled = AffectionEnabledBox.IsChecked == true,
            InteractionBubbleProbabilityPercent = interactionProbability,
            CheckIntervalSeconds = interval,
            ProbabilityPercent = probability,
            CooldownSeconds = cooldown,
            BubbleSeconds = duration,
            Lines = _lines.Select(x => new DialogueLine { Enabled = x.Enabled, Text = x.Text, Context = x.Context }).ToList(),
            Scale = _source.Scale,
            Speed = _source.Speed,
            Topmost = _source.Topmost,
            AutoRoutine = _source.AutoRoutine,
            ActionId = _source.ActionId,
            SkinId = _source.SkinId,
            Left = _source.Left,
            Top = _source.Top
        };
        ResultSettings.Normalize();
        DialogResult = true;
    }

    private bool ReadNumber(TextBox box, int min, int max, out int value)
    {
        if (int.TryParse(box.Text, out value) && value >= min && value <= max) return true;
        MessageBox.Show(this, $"请输入 {min} 到 {max} 之间的整数。", "设置数值无效");
        box.Focus();
        box.SelectAll();
        return false;
    }
}
