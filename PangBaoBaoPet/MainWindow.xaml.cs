using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PangBaoBaoPet;

public partial class MainWindow : Window
{
    private readonly List<AnimationAction> _actions;
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _bubbleTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _actionWatch = new();
    private readonly Random _random = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly BubbleScheduler _scheduler;
    private readonly AffectionService _affection;
    private readonly List<string> _bubblePages = new();
    private AppSettings _settings;
    private AnimationAction _action;
    private AnimationAction? _resumeAction;
    private MenuItem? _affectionMenu;
    private string? _firstRewardPlaying;
    private bool _advanceRoutineAfterSpecial;
    private bool _paused;
    private bool _pointerDown;
    private Point _pressPoint;
    private int _frameIndex = -1;
    private long _lastCycle;
    private int _bubblePage;
    private int _bubblePriority;
    private DateTimeOffset _bubbleEnds;
    private DateTimeOffset _lastHeadReactionAt;
    private DateTimeOffset _lastInteractionBubbleAt;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load(out var settingsWarning);
        _affection = new AffectionService(PetStateStore.Load(out var stateWarning));
        _actions = AnimationCatalog.Load().ToList();
        _action = _actions.FirstOrDefault(a => a.Id == _settings.ActionId && !a.OneShot)
            ?? _actions.First(a => a.IncludeInRoutine);
        _scheduler = new BubbleScheduler(DateTimeOffset.Now, _settings);
        SetFrame(0);
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
            if (settingsWarning is not null || stateWarning is not null)
                MessageBox.Show(this, string.Join("\n", new[] { settingsWarning, stateWarning }.Where(x => x is not null)), "胖宝宝桌宠");
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _actionWatch.Stop();
                if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
            }
            else if (!_paused)
            {
                _actionWatch.Start();
                _scheduler.Reset(DateTimeOffset.Now, _settings);
            }
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
        foreach (var action in _actions.Where(a => !a.OneShot))
        {
            var item = new MenuItem { Header = $"单节循环：{action.Name}" };
            item.Click += (_, _) =>
            {
                _settings.AutoRoutine = false;
                routine.IsChecked = false;
                _resumeAction = null;
                _firstRewardPlaying = null;
                SetAction(action, true);
                SaveSettings();
            };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        _affectionMenu = new MenuItem { IsEnabled = false };
        UpdateAffectionMenu();
        menu.Items.Add(_affectionMenu);
        var previews = new MenuItem { Header = "预览特殊动作" };
        foreach (var id in new[] { "shy", "kiss", "roll" })
        {
            var action = _actions.First(a => a.Id == id);
            var item = new MenuItem { Header = action.Name };
            item.Click += (_, _) => StartSpecial(id, firstReward: false, advanceRoutine: false);
            previews.Items.Add(item);
        }
        menu.Items.Add(previews);
        var reset = new MenuItem { Header = "重置好感度…" };
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "清除好感度、解锁和首次动作记录？", "重置好感度",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var state = _affection.State;
            state.Affection = 0;
            state.PendingMilestones.Clear();
            state.CompletedMilestones.Clear();
            state.ScoreWindowPoints = 0;
            state.ScoreWindowStartedAt = null;
            state.LastClickAt = null;
            state.LastDragAt = null;
            state.LastSpecialAt = null;
            _firstRewardPlaying = null;
            if (_action.OneShot)
            {
                var resume = _resumeAction ?? _actions.First(a => a.IncludeInRoutine);
                _resumeAction = null;
                _advanceRoutineAfterSpecial = false;
                SetAction(resume, false);
            }
            SavePetState();
            UpdateAffectionMenu();
        };
        menu.Items.Add(reset);
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
        var settings = new MenuItem { Header = "设置对话与互动…" };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => Exit();
        menu.Items.Add(exit);
        PetImage.ContextMenu = menu;
    }

    private void UpdateAffectionMenu()
    {
        if (_affectionMenu is not null)
            _affectionMenu.Header = $"好感度 {_affection.State.Affection}/100  ·  飞吻 30  ·  打滚 70";
    }

    private void SetAction(AnimationAction action, bool persistSelection)
    {
        _action = action;
        if (persistSelection) _settings.ActionId = action.Id;
        _actionWatch.Restart();
        if (_paused) _actionWatch.Stop();
        _lastCycle = 0;
        _frameIndex = -1;
        SetFrame(0);
    }

    private void SetFrame(int index)
    {
        _frameIndex = index;
        PetImage.Source = _action.Frames[index];
        var height = _action.FrameDisplayHeights[index];
        PetImage.Height = height;
        PetImage.Width = height * _action.Frames[index].PixelWidth / _action.Frames[index].PixelHeight;
        if (SpeechBubble.Visibility == Visibility.Visible) Dispatcher.BeginInvoke(new Action(PlaceBubble), DispatcherPriority.Loaded);
    }

    private AnimationAction NextRoutine(AnimationAction current)
    {
        var routine = _actions.Where(a => a.IncludeInRoutine && !a.OneShot).ToArray();
        var index = Array.IndexOf(routine, current);
        return routine[(index + 1) % routine.Length];
    }

    private void AdvanceFrame()
    {
        if (_paused || !IsVisible) return;
        var elapsed = _actionWatch.Elapsed.TotalSeconds * _settings.Speed;
        if (_action.OneShot && elapsed >= _action.Duration)
        {
            FinishSpecial();
            return;
        }
        if (!_action.OneShot)
        {
            var cycle = (long)(elapsed / _action.Duration);
            if (cycle > _lastCycle)
            {
                _lastCycle = cycle;
                if (TryStartPending()) return;
                if (_settings.AutoRoutine)
                {
                    SetAction(NextRoutine(_action), false);
                    return;
                }
            }
        }
        var index = _action.FrameAt(elapsed);
        if (index == _frameIndex) return;
        SetFrame(index);
        if (_action.Id == "kiss" && index == 2) SpawnHeart(0);
    }

    private bool TryStartPending()
    {
        if (!_settings.AffectionEnabled || _paused || !IsVisible) return false;
        var id = _affection.NextPending();
        return id is not null && StartSpecial(id, firstReward: true, advanceRoutine: _settings.AutoRoutine);
    }

    private bool StartSpecial(string id, bool firstReward, bool advanceRoutine)
    {
        if (_paused || !IsVisible) return false;
        var special = _actions.FirstOrDefault(a => a.Id == id && a.OneShot);
        if (special is null) return false;
        if (_action.OneShot) return false;
        _resumeAction ??= _action;
        _firstRewardPlaying = firstReward ? id : null;
        _advanceRoutineAfterSpecial = advanceRoutine;
        if (id is "kiss" or "roll")
        {
            _affection.RecordSpecial(DateTimeOffset.Now);
            SavePetState();
            if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
        }
        SetAction(special, false);
        return true;
    }

    private void FinishSpecial()
    {
        var finished = _action.Id;
        if (_firstRewardPlaying is { } reward)
        {
            _affection.Complete(reward);
            SavePetState();
        }
        _firstRewardPlaying = null;
        var resume = _resumeAction ?? _actions.First(a => a.IncludeInRoutine);
        if (_advanceRoutineAfterSpecial && _settings.AutoRoutine) resume = NextRoutine(resume);
        _resumeAction = null;
        _advanceRoutineAfterSpecial = false;
        SetAction(resume, false);
        if (finished is "kiss" or "roll") MaybeShowContext(finished, forced: true);
    }

    private void AdvanceBubble()
    {
        var now = DateTimeOffset.Now;
        if (SpeechBubble.Visibility == Visibility.Visible && now >= _bubbleEnds)
        {
            if (_bubblePage + 1 < _bubblePages.Count)
            {
                _bubblePage++;
                SpeechText.Text = _bubblePages[_bubblePage];
                _bubbleEnds = now.AddSeconds(_settings.BubbleSeconds);
                PlaceBubble();
            }
            else HideBubble();
        }
        var text = _scheduler.Tick(now, _settings, _random, IsVisible && !_paused && !_action.OneShot);
        if (text is not null) ShowBubble(text, 1);
    }

    private void MaybeShowContext(string context, bool forced = false)
    {
        if (!_settings.BubbleEnabled || _paused || !IsVisible) return;
        var now = DateTimeOffset.Now;
        if (!forced)
        {
            if (now - _lastInteractionBubbleAt < TimeSpan.FromSeconds(20) ||
                _random.Next(100) >= _settings.InteractionBubbleProbabilityPercent) return;
        }
        var lines = _settings.Lines.Where(x => x.Enabled && x.Context == context && !string.IsNullOrWhiteSpace(x.Text)).ToArray();
        if (lines.Length == 0) return;
        if (ShowBubble(lines[_random.Next(lines.Length)].Text, forced ? 3 : 2))
            _lastInteractionBubbleAt = now;
    }

    private bool ShowBubble(string text, int priority)
    {
        if (string.IsNullOrWhiteSpace(text) || SpeechBubble.Visibility == Visibility.Visible && _bubblePriority > priority)
            return false;
        if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
        _bubblePages.Clear();
        _bubblePages.AddRange(Paginate(text));
        _bubblePage = 0;
        _bubblePriority = priority;
        SpeechText.Text = _bubblePages[0];
        _scheduler.Preview();
        SpeechBubble.Visibility = Visibility.Visible;
        BubbleTail.Visibility = Visibility.Visible;
        _bubbleEnds = DateTimeOffset.Now.AddSeconds(_settings.BubbleSeconds);
        PlaceBubble();
        return true;
    }

    private static IEnumerable<string> Paginate(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text.Trim());
        var page = "";
        var elements = 0;
        var lineBreaks = 0;
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (elements >= 32 || lineBreaks >= 3)
            {
                yield return page;
                page = "";
                elements = 0;
                lineBreaks = 0;
            }
            page += element;
            elements++;
            if (element == "\n") lineBreaks++;
        }
        if (page.Length > 0) yield return page;
    }

    private void PlaceBubble()
    {
        if (SpeechBubble.Visibility != Visibility.Visible) return;
        SpeechBubble.Measure(new Size(260, double.PositiveInfinity));
        var height = SpeechBubble.DesiredSize.Height;
        var head = PetImage.TranslatePoint(new Point(PetImage.ActualWidth * _action.HeadX,
            PetImage.ActualHeight * _action.HeadY), Root);
        if (!double.IsFinite(head.X) || !double.IsFinite(head.Y)) head = new Point(280, 155);
        const double width = 260;
        var area = GetCurrentWorkArea();
        var preferLeft = Left + Width / 2 > area.Left + area.Width / 2;
        var left = preferLeft ? head.X - width - 28 : head.X + 28;
        left = Math.Clamp(left, 8, Root.Width - width - 8);
        var top = Math.Clamp(head.Y - height - 38, 8, Root.Height - height - 8);
        Canvas.SetLeft(SpeechBubble, left);
        Canvas.SetTop(SpeechBubble, top);
        var baseX = head.X > left + width / 2 ? left + width - 35 : left + 35;
        var bottom = top + height - 2;
        var figure = new PathFigure(new Point(baseX - 11, bottom), new PathSegment[]
        {
            new LineSegment(new Point(head.X, Math.Max(bottom + 5, head.Y - 8)), true),
            new LineSegment(new Point(baseX + 11, bottom), true)
        }, true);
        BubbleTail.Data = new PathGeometry(new[] { figure });
    }

    private void HideBubble()
    {
        SpeechBubble.Visibility = Visibility.Collapsed;
        BubbleTail.Visibility = Visibility.Collapsed;
        _bubblePriority = 0;
        _scheduler.Dismiss(DateTimeOffset.Now, _settings);
    }

    private void PreviewBubble()
    {
        var lines = _settings.Lines.Where(line => line.Enabled && !string.IsNullOrWhiteSpace(line.Text)).ToArray();
        if (lines.Length == 0) { MessageBox.Show(this, "先在设置中添加并启用文案。", "没有可用文案"); return; }
        ShowBubble(lines[_random.Next(lines.Length)].Text, 4);
    }

    private void SpawnHeart(int points)
    {
        var head = PetImage.TranslatePoint(new Point(PetImage.ActualWidth * 0.5,
            PetImage.ActualHeight * 0.14), Root);
        var effect = new TextBlock
        {
            Text = points > 0 ? $"♥ +{points}" : "♥",
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(239, 83, 125)),
            IsHitTestVisible = false
        };
        EffectsLayer.Children.Add(effect);
        var bubbleCenter = SpeechBubble.Visibility == Visibility.Visible
            ? Canvas.GetLeft(SpeechBubble) + SpeechBubble.Width / 2 : head.X - 1;
        var effectLeft = bubbleCenter < head.X ? head.X + 42 : head.X - 112;
        Canvas.SetLeft(effect, Math.Clamp(effectLeft, 8, Root.Width - 85));
        Canvas.SetTop(effect, head.Y - 20);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1600));
        fade.Completed += (_, _) => EffectsLayer.Children.Remove(effect);
        effect.BeginAnimation(OpacityProperty, fade);
        effect.BeginAnimation(Canvas.TopProperty,
            new DoubleAnimation(head.Y - 20, head.Y - 75, TimeSpan.FromMilliseconds(1600)));
    }

    private void RegisterInteraction(PetInteraction kind, bool headClick)
    {
        if (_paused || !IsVisible) return;
        var now = DateTimeOffset.Now;
        var points = 0;
        if (_settings.AffectionEnabled)
        {
            var result = _affection.Register(kind, now);
            points = result.Points;
            if (points > 0)
            {
                SavePetState();
                UpdateAffectionMenu();
            }
        }
        if (_action.OneShot) return;
        if (kind == PetInteraction.Click && headClick &&
            now - _lastHeadReactionAt >= TimeSpan.FromMilliseconds(1200))
        {
            _lastHeadReactionAt = now;
            SpawnHeart(points);
            if (!_action.OneShot) StartSpecial("shy", firstReward: false, advanceRoutine: false);
        }
        else if (points > 0) SpawnHeart(points);
        if (points > 0 && (kind == PetInteraction.Drag || headClick))
            MaybeShowContext(kind == PetInteraction.Click ? "click" : "drag");
        if (points > 0 && _affection.NextPending() is null && !_action.OneShot &&
            _affection.CanPlayOccasional(now) && _random.Next(100) < 10)
        {
            var id = _affection.State.CompletedMilestones[_random.Next(_affection.State.CompletedMilestones.Count)];
            StartSpecial(id, firstReward: false, advanceRoutine: false);
        }
    }

    private static bool IsOpaqueAt(Image image, Point position)
    {
        if (image.Source is not BitmapSource bitmap || image.ActualWidth <= 0 || image.ActualHeight <= 0) return false;
        var x = (int)(position.X * bitmap.PixelWidth / image.ActualWidth);
        var y = (int)(position.Y * bitmap.PixelHeight / image.ActualHeight);
        if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight) return false;
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3] > 35;
    }

    private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _pointerDown = false;
            PetImage.ReleaseMouseCapture();
            PreviewBubble();
            e.Handled = true;
            return;
        }
        _pressPoint = e.GetPosition(PetImage);
        if (!IsOpaqueAt(PetImage, _pressPoint)) return;
        _pointerDown = true;
        PetImage.CaptureMouse();
        e.Handled = true;
    }

    private void PetImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pointerDown) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _pointerDown = false;
            PetImage.ReleaseMouseCapture();
            return;
        }
        var current = e.GetPosition(PetImage);
        if (Math.Abs(current.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _pointerDown = false;
        PetImage.ReleaseMouseCapture();
        var oldLeft = Left;
        var oldTop = Top;
        var wasRunning = _actionWatch.IsRunning;
        _actionWatch.Stop();
        try { DragMove(); }
        catch (InvalidOperationException) { }
        finally { if (wasRunning && !_paused) _actionWatch.Start(); }
        if (Math.Abs(Left - oldLeft) + Math.Abs(Top - oldTop) < 3) return;
        ClampToWorkArea();
        _settings.Left = Left;
        _settings.Top = Top;
        SaveSettings();
        RegisterInteraction(PetInteraction.Drag, headClick: false);
        if (SpeechBubble.Visibility == Visibility.Visible) PlaceBubble();
    }

    private void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pointerDown) return;
        _pointerDown = false;
        PetImage.ReleaseMouseCapture();
        var point = e.GetPosition(PetImage);
        if (!IsOpaqueAt(PetImage, point)) return;
        var headClick = point.Y < PetImage.ActualHeight * 0.35 &&
            point.X > PetImage.ActualWidth * 0.25 && point.X < PetImage.ActualWidth * 0.75;
        RegisterInteraction(PetInteraction.Click, headClick);
        e.Handled = true;
    }

    private void SpeechBubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HideBubble();
        e.Handled = true;
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_settings, _affection.State.Affection) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultSettings is null) return;
        var left = _settings.Left;
        var top = _settings.Top;
        _settings = dialog.ResultSettings;
        _settings.Left = left;
        _settings.Top = top;
        Topmost = _settings.Topmost;
        ApplyScale();
        _scheduler.Reset(DateTimeOffset.Now, _settings);
        if (!_settings.BubbleEnabled && SpeechBubble.Visibility == Visibility.Visible) HideBubble();
        SaveSettings();
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_paused)
        {
            _actionWatch.Stop();
            if (SpeechBubble.Visibility == Visibility.Visible) HideBubble();
        }
        else
        {
            _actionWatch.Start();
            _scheduler.Reset(DateTimeOffset.Now, _settings);
        }
    }

    private void ApplyScale()
    {
        Root.LayoutTransform = new ScaleTransform(_settings.Scale, _settings.Scale);
        Width = 430 * _settings.Scale;
        Height = 490 * _settings.Scale;
        ClampToWorkArea();
        if (SpeechBubble.Visibility == Visibility.Visible) PlaceBubble();
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

    private void SaveSettings()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, $"保存设置失败：{ex.Message}", "胖宝宝桌宠"); }
    }

    private void SavePetState()
    {
        try { PetStateStore.Save(_affection.State); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, $"保存好感度失败：{ex.Message}", "胖宝宝桌宠"); }
    }

    private void Exit() => Close();

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _frameTimer.Stop();
        _bubbleTimer.Stop();
        SavePetState();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnClosed(e);
    }
}
