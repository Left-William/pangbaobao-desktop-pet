using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PangBaoBaoPet;

public partial class MainWindow : Window
{
    private readonly List<AnimationAction> _actions;
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _bubbleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _actionWatch = new();
    private readonly Random _random = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly BubbleScheduler _scheduler;
    private AppSettings _settings;
    private AnimationAction _action;
    private int _frameIndex = -1;
    private bool _paused;
    private DateTimeOffset _bubbleEnds;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load(out var warning);
        _actions = AnimationCatalog.Load().ToList();
        _action = _actions.FirstOrDefault(a => a.Id == _settings.ActionId) ?? _actions[0];
        SizePetImage();
        _scheduler = new BubbleScheduler(DateTimeOffset.Now, _settings);
        PetImage.Source = _action.Frames[0];
        BuildContextMenu();
        _tray = BuildTray();
        _frameTimer.Tick += (_, _) => AdvanceFrame();
        _bubbleTimer.Tick += (_, _) => AdvanceBubble();
        _frameTimer.Start();
        _bubbleTimer.Start();
        _actionWatch.Start();
        Topmost = _settings.Topmost;
        ApplyScale();
        Loaded += (_, _) =>
        {
            RestorePosition();
            if (warning is not null) MessageBox.Show(this, warning, "胖宝宝桌宠");
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private Forms.NotifyIcon BuildTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示桌宠", null, (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); _scheduler.Reset(DateTimeOffset.Now, _settings); }));
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("暂停 / 继续", null, (_, _) => Dispatcher.Invoke(TogglePause));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Exit));
        var tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "pet.ico")),
            Text = "胖宝宝桌宠",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); });
        return tray;
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        var routine = new MenuItem { Header = "整套广播体操", IsCheckable = true, IsChecked = _settings.AutoRoutine };
        routine.Click += (_, _) => { _settings.AutoRoutine = routine.IsChecked; SaveSettings(); };
        menu.Items.Add(routine);
        foreach (var action in _actions)
        {
            var item = new MenuItem { Header = $"单节循环：{action.Name}" };
            item.Click += (_, _) => { _settings.AutoRoutine = false; routine.IsChecked = false; SetAction(action); SaveSettings(); };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var pause = new MenuItem { Header = "暂停 / 继续" };
        pause.Click += (_, _) => TogglePause();
        menu.Items.Add(pause);
        var topmost = new MenuItem { Header = "保持置顶", IsCheckable = true, IsChecked = _settings.Topmost };
        topmost.Click += (_, _) => { _settings.Topmost = topmost.IsChecked; Topmost = _settings.Topmost; SaveSettings(); };
        menu.Items.Add(topmost);
        var scaleMenu = new MenuItem { Header = "缩放" };
        foreach (var scale in new[] { 0.75, 1.0, 1.25, 1.5 })
        {
            var item = new MenuItem { Header = $"{scale * 100:0}%" };
            item.Click += (_, _) => { _settings.Scale = scale; ApplyScale(); SaveSettings(); };
            scaleMenu.Items.Add(item);
        }
        menu.Items.Add(scaleMenu);
        var speedMenu = new MenuItem { Header = "动作速度" };
        foreach (var speed in new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 })
        {
            var item = new MenuItem { Header = $"{speed:0.##} 倍" };
            item.Click += (_, _) => { _settings.Speed = speed; SaveSettings(); };
            speedMenu.Items.Add(item);
        }
        menu.Items.Add(speedMenu);
        var preview = new MenuItem { Header = "预览一条气泡" };
        preview.Click += (_, _) => PreviewBubble();
        menu.Items.Add(preview);
        var settings = new MenuItem { Header = "设置对话气泡…" };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => Exit();
        menu.Items.Add(exit);
        PetImage.ContextMenu = menu;
    }

    private void SetAction(AnimationAction action)
    {
        _action = action;
        SizePetImage();
        _settings.ActionId = action.Id;
        _actionWatch.Restart();
        if (_paused) _actionWatch.Stop();
        _frameIndex = -1;
        AdvanceFrame();
    }

    private void AdvanceFrame()
    {
        if (_paused) return;
        var elapsed = _actionWatch.Elapsed.TotalSeconds * _settings.Speed;
        if (_settings.AutoRoutine && elapsed >= _action.Duration)
        {
            var routine = _actions.Where(a => a.IncludeInRoutine).ToList();
            var current = routine.IndexOf(_action);
            SetAction(routine[(current + 1) % routine.Count]);
            return;
        }
        var index = _action.FrameAt(elapsed);
        if (index == _frameIndex) return;
        _frameIndex = index;
        PetImage.Source = _action.Frames[index];
    }

    private void SizePetImage()
    {
        PetImage.Height = _action.DisplayHeight;
        PetImage.Width = _action.DisplayHeight * _action.Frames[0].PixelWidth / _action.Frames[0].PixelHeight;
    }

    private void AdvanceBubble()
    {
        var now = DateTimeOffset.Now;
        if (SpeechBubble.Visibility == Visibility.Visible && now >= _bubbleEnds) HideBubble();
        var text = _scheduler.Tick(now, _settings, _random, IsVisible && !_paused);
        if (text is not null) ShowBubble(text);
    }

    private void ShowBubble(string text)
    {
        SpeechText.Text = text;
        var area = GetCurrentWorkArea();
        SpeechBubble.HorizontalAlignment = Left + Width / 2 > area.Left + area.Width / 2
            ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        SpeechBubble.Margin = SpeechBubble.HorizontalAlignment == HorizontalAlignment.Left
            ? new Thickness(12, 0, 0, 5) : new Thickness(0, 0, 12, 5);
        SpeechBubble.Visibility = Visibility.Visible;
        _bubbleEnds = DateTimeOffset.Now.AddSeconds(_settings.BubbleSeconds);
    }

    private void HideBubble()
    {
        SpeechBubble.Visibility = Visibility.Collapsed;
        _scheduler.Dismiss(DateTimeOffset.Now, _settings);
    }

    private void PreviewBubble()
    {
        var lines = _settings.Lines.Where(line => line.Enabled && !string.IsNullOrWhiteSpace(line.Text)).ToArray();
        if (lines.Length == 0) { MessageBox.Show(this, "先在设置中添加并启用文案。", "没有可用文案"); return; }
        if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
        _scheduler.Preview();
        ShowBubble(lines[_random.Next(lines.Length)].Text);
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null) return;
        var left = _settings.Left;
        var top = _settings.Top;
        _settings = dialog.ResultSettings;
        _settings.Left = left;
        _settings.Top = top;
        Topmost = _settings.Topmost;
        ApplyScale();
        _scheduler.Reset(DateTimeOffset.Now, _settings);
        SaveSettings();
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_paused) _actionWatch.Stop(); else { _actionWatch.Start(); _scheduler.Reset(DateTimeOffset.Now, _settings); }
    }

    private void ApplyScale()
    {
        Root.LayoutTransform = new ScaleTransform(_settings.Scale, _settings.Scale);
        Width = 390 * _settings.Scale;
        Height = 490 * _settings.Scale;
        ClampToWorkArea();
    }

    private void RestorePosition()
    {
        var area = GetCurrentWorkArea();
        Left = _settings.Left ?? area.Right - Width - 40;
        Top = _settings.Top ?? area.Bottom - Height - 20;
        ClampToWorkArea();
    }

    private void ClampToWorkArea()
    {
        if (!IsLoaded) return;
        var area = GetCurrentWorkArea();
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    private Rect GetCurrentWorkArea()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
            return SystemParameters.WorkArea;
        var screen = Forms.Screen.FromHandle(source.Handle);
        var transform = source.CompositionTarget.TransformFromDevice;
        var bounds = screen.WorkingArea;
        return new Rect(bounds.Left * transform.M11, bounds.Top * transform.M22,
            bounds.Width * transform.M11, bounds.Height * transform.M22);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.Invoke(ClampToWorkArea);

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
            _scheduler.Reset(DateTimeOffset.Now, _settings);
            _actionWatch.Restart();
            if (_paused) _actionWatch.Stop();
        }));
    }

    private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { PreviewBubble(); return; }
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        _settings.Left = Left;
        _settings.Top = Top;
        SaveSettings();
    }

    private void SpeechBubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HideBubble();
        e.Handled = true;
    }

    private void SaveSettings()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, $"保存设置失败：{ex.Message}", "胖宝宝桌宠"); }
    }

    private void Exit() => Close();

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _frameTimer.Stop();
        _bubbleTimer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnClosed(e);
    }
}
