using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace PangBaoBaoPet;

public partial class ConversationWindow : Window
{
    private readonly string _actionId;
    private readonly int _affection;
    private readonly IReadOnlyCollection<string> _installedActions;
    private readonly Action<PetReply> _onReply;
    private readonly HttpClient _client = new();
    private readonly List<DialogueTurn> _history = new();
    private CancellationTokenSource? _request;
    private int _configurationRevision;

    public ConversationWindow(string actionId, int affection, IReadOnlyCollection<string> installedActions, Action<PetReply> onReply)
    {
        InitializeComponent();
        _actionId = actionId;
        _affection = affection;
        _installedActions = installedActions;
        _onReply = onReply;
        var options = DialogueApiStore.Load();
        EnabledBox.IsChecked = options.Enabled;
        EndpointBox.Text = options.EndpointUrl;
        ModelBox.Text = options.Model;
        AllowActionsBox.IsChecked = options.AllowActionSuggestions;
        AutoRepliesBox.IsChecked = options.AutomaticReplies;
        PersonaBox.Text = PersonaStore.Load();
        ResponseBox.Text = "胖宝宝：我在呢。输入文字后点发送；没填 API 时，桌宠仍会按本地台词说话。";
        Closed += (_, _) => { _request?.Cancel(); _request?.Dispose(); _client.Dispose(); };
    }

    private DialogueApiOptions ReadOptions()
    {
        var options = DialogueApiStore.Load();
        options.Enabled = EnabledBox.IsChecked == true;
        options.EndpointUrl = EndpointBox.Text;
        options.Model = ModelBox.Text;
        options.AllowActionSuggestions = AllowActionsBox.IsChecked == true;
        options.AutomaticReplies = AutoRepliesBox.IsChecked == true;
        return options;
    }

    private bool SaveCurrent(bool includePersona)
    {
        try
        {
            var options = ReadOptions();
            if (!options.TryValidate(out var error)) throw new InvalidDataException(error);
            DialogueApiStore.Save(options);
            if (KeyBox.Password.Length > 0)
            {
                DialogueApiStore.SaveKey(KeyBox.Password);
                KeyBox.Clear();
            }
            if (includePersona) PersonaStore.Save(PersonaBox.Text);
            _configurationRevision++;
            _request?.Cancel();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or CryptographicException)
        {
            MessageBox.Show(this, ex.Message, "保存对话配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (SaveCurrent(includePersona: false))
            MessageBox.Show(this, "对话配置已保存。", "胖宝宝桌宠");
    }

    private void SavePersona_Click(object sender, RoutedEventArgs e)
    {
        try { PersonaStore.Save(PersonaBox.Text); _configurationRevision++; _request?.Cancel(); MessageBox.Show(this, "人设已保存。", "胖宝宝桌宠"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageBox.Show(this, ex.Message, "保存人设失败"); }
    }

    private void ResetPersona_Click(object sender, RoutedEventArgs e)
    {
        PersonaBox.Text = PersonaStore.DefaultText;
        SavePersona_Click(sender, e);
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveCurrent(includePersona: true)) return;
        await AskAsync("你好，请用一句话打个招呼。", isTest: true);
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveCurrent(includePersona: true)) return;
        await AskAsync(InputBox.Text, isTest: false);
    }

    private async System.Threading.Tasks.Task AskAsync(string text, bool isTest)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_request is not null) return;
        var options = DialogueApiStore.Load();
        if (!options.Enabled && !isTest)
        {
            ShowLocalLine("未启用外部 API");
            return;
        }
        try
        {
            var key = DialogueApiStore.LoadKey();
            if (key is null) throw new InvalidOperationException("请在 API 设置里填写并保存密钥。");
            _request = new CancellationTokenSource();
            var revision = _configurationRevision;
            SendButton.IsEnabled = false;
            if (!isTest) AppendTranscript("你：" + text.Trim());
            AppendTranscript("胖宝宝：正在想……");
            DialogueUsageStore.RecordManual(isTest, DateTimeOffset.Now);
            var reply = await new DialogueService(_client).SendAsync(options, key, PersonaStore.Load(), text,
                isTest ? "connection-test" : "manual-chat", _actionId, _affection, _request.Token,
                isTest ? null : _history, _installedActions);
            if (revision != _configurationRevision || _request.IsCancellationRequested)
            {
                ClearThinking();
                return;
            }
            ClearThinking();
            AppendTranscript("胖宝宝：" + reply.Text);
            if (!isTest)
            {
                _history.Add(new DialogueTurn("user", text.Trim()));
                _history.Add(new DialogueTurn("assistant", reply.Text));
                while (_history.Count > 12) _history.RemoveRange(0, 2);
                _onReply(reply);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or
            InvalidOperationException or ArgumentException or CryptographicException or OperationCanceledException)
        {
            ClearThinking();
            AppendTranscript(ex is OperationCanceledException ? "请求已取消或超时。" : "对话服务暂不可用：" + ex.Message);
            if (!isTest && ex is not OperationCanceledException) ShowLocalLine("服务不可用");
        }
        finally
        {
            _request?.Dispose();
            _request = null;
            SendButton.IsEnabled = true;
        }
    }

    private void AppendTranscript(string line)
    {
        ResponseBox.Text += "\n" + line;
        if (ResponseBox.Text.Length > 12000) ResponseBox.Text = ResponseBox.Text[^12000..];
        ResponseBox.ScrollToEnd();
    }

    private void ClearThinking()
    {
        const string marker = "\n胖宝宝：正在想……";
        if (ResponseBox.Text.EndsWith(marker, StringComparison.Ordinal))
            ResponseBox.Text = ResponseBox.Text[..^marker.Length];
    }

    private void ShowLocalLine(string reason)
    {
        var local = SettingsStore.Load(out _).Lines.FirstOrDefault(line => line.Enabled &&
            line.Context == "ambient" && !string.IsNullOrWhiteSpace(line.Text))?.Text;
        if (local is null) return;
        AppendTranscript($"[本地台词·{reason}] 胖宝宝：{local}");
        _onReply(new PetReply(local, "neutral", "none"));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _request?.Cancel();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
