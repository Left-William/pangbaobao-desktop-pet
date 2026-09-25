using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PangBaoBaoPet;

public sealed class DialogueApiOptions
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; }
    public string Adapter { get; set; } = "chat-completions-compatible";
    public string EndpointUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxOutputTokens { get; set; } = 160;
    public double Temperature { get; set; } = 0.7;
    public bool AutomaticReplies { get; set; }
    public int AutoReplyMinIntervalSeconds { get; set; } = 120;
    public int AutomaticDailyRequestLimit { get; set; } = 30;
    public bool AllowActionSuggestions { get; set; }

    public void Normalize()
    {
        Adapter = "chat-completions-compatible";
        EndpointUrl = (EndpointUrl ?? "").Trim();
        Model = (Model ?? "").Trim();
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 60);
        MaxOutputTokens = Math.Clamp(MaxOutputTokens, 32, 512);
        Temperature = double.IsFinite(Temperature) ? Math.Clamp(Temperature, 0, 2) : 0.7;
        AutoReplyMinIntervalSeconds = Math.Clamp(AutoReplyMinIntervalSeconds, 30, 3600);
        AutomaticDailyRequestLimit = Math.Clamp(AutomaticDailyRequestLimit, 0, 100);
    }

    public bool TryValidate(out string error)
    {
        Normalize();
        if (!Enabled) { error = ""; return true; }
        if (Model.Length is < 1 or > 120) { error = "请填写模型名称（最多 120 字）。"; return false; }
        if (EndpointUrl.Length > 2048 || !Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var uri))
        { error = "请填写完整的 API 地址。"; return false; }
        if (uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 ||
            uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        { error = "API 地址须使用 HTTPS；本机回环地址可使用 HTTP，且不能含账号或片段。"; return false; }
        error = "";
        return true;
    }
}

public static class DialogueApiStore
{
    public static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "dialogue-api.json");
    public static string KeyPath => Path.Combine(SettingsStore.DirectoryPath, "dialogue-api.key.dpapi");

    public static DialogueApiOptions Load()
    {
        if (!File.Exists(FilePath)) return new DialogueApiOptions();
        try
        {
            var options = JsonSerializer.Deserialize<DialogueApiOptions>(File.ReadAllText(FilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            options.Normalize();
            return options;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DialogueApiOptions();
        }
    }

    public static void Save(DialogueApiOptions options)
    {
        options.Normalize();
        if (!options.TryValidate(out var error)) throw new InvalidDataException(error);
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, true);
    }

    public static void SaveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("密钥不能为空。", nameof(key));
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        var bytes = Encoding.UTF8.GetBytes(key.Trim());
        try
        {
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var tmp = KeyPath + ".tmp";
            File.WriteAllBytes(tmp, encrypted);
            File.Move(tmp, KeyPath, true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static string? LoadKey()
    {
        if (!File.Exists(KeyPath)) return null;
        var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(decrypted); }
        finally { CryptographicOperations.ZeroMemory(decrypted); }
    }
}

public sealed class DialogueUsage
{
    public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public int AutomaticRequests { get; set; }
    public int ManualRequests { get; set; }
    public int ConnectionTests { get; set; }
    public DateTimeOffset? LastAutomaticAt { get; set; }
    public DateTimeOffset? AutomaticCooldownUntil { get; set; }
}

public static class DialogueUsageStore
{
    private static readonly object Gate = new();
    public static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "dialogue-usage.json");

    public static DialogueUsage Load()
    {
        lock (Gate) return Read(DateTimeOffset.Now);
    }

    public static bool TryReserveAutomatic(DialogueApiOptions options, DateTimeOffset now)
    {
        if (!options.Enabled || !options.AutomaticReplies) return false;
        options.Normalize();
        lock (Gate)
        {
            var usage = Read(now);
            if (usage.AutomaticRequests >= options.AutomaticDailyRequestLimit ||
                now < usage.AutomaticCooldownUntil ||
                usage.LastAutomaticAt is { } last && now - last < TimeSpan.FromSeconds(options.AutoReplyMinIntervalSeconds))
                return false;
            usage.AutomaticRequests++;
            usage.LastAutomaticAt = now;
            Write(usage);
            return true;
        }
    }

    public static void RecordManual(bool connectionTest, DateTimeOffset now)
    {
        lock (Gate)
        {
            var usage = Read(now);
            if (connectionTest) usage.ConnectionTests++;
            else usage.ManualRequests++;
            Write(usage);
        }
    }

    public static void SetAutomaticCooldown(DateTimeOffset until)
    {
        lock (Gate)
        {
            var usage = Read(DateTimeOffset.Now);
            if (usage.AutomaticCooldownUntil is null || until > usage.AutomaticCooldownUntil)
                usage.AutomaticCooldownUntil = until;
            Write(usage);
        }
    }

    private static DialogueUsage Read(DateTimeOffset now)
    {
        DialogueUsage usage;
        try
        {
            usage = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<DialogueUsage>(File.ReadAllText(FilePath)) ?? new DialogueUsage()
                : new DialogueUsage();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("对话次数记录无法读取，自动请求已停止。", ex);
        }
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        if (usage.Day != today)
        {
            usage.Day = today;
            usage.AutomaticRequests = 0;
            usage.ManualRequests = 0;
            usage.ConnectionTests = 0;
        }
        return usage;
    }

    private static void Write(DialogueUsage usage)
    {
        Directory.CreateDirectory(SettingsStore.DirectoryPath);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, true);
    }
}

public static class PersonaStore
{
    private static readonly Regex Placeholder = new(@"\{\{([a-z_]+)\}\}", RegexOptions.Compiled);
    public static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "prompts", "persona.md");
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Defaults", "persona.md");

    public static string DefaultText => File.Exists(DefaultPath) ? File.ReadAllText(DefaultPath) :
        "你是胖宝宝，像熟悉的舍友一样用简短自然的中文说话。可以轻轻吐槽，别编造现实经历。";

    public static string Load() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : DefaultText;

    public static string Render(string template, string eventType, string actionId, int affection,
        IReadOnlyCollection<string> allowedActions, string nickname = "你", string teasingLevel = "playful")
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["event_type"] = eventType,
            ["current_action"] = actionId,
            ["affection_band"] = affection switch { < 30 => "初识", < 70 => "熟悉", _ => "亲近" },
            ["allowed_actions"] = string.Join(", ", allowedActions),
            ["user_nickname"] = nickname,
            ["teasing_level"] = teasingLevel
        };
        return Placeholder.Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    public static void Save(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 8000)
            throw new ArgumentException("人设不能为空，且最多 8000 字。", nameof(text));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
        File.WriteAllText(FilePath + ".tmp", text, Encoding.UTF8);
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}

public sealed record PetReply(string Text, string Emotion, string Action);
public sealed record DialogueTurn(string Role, string Content);

public sealed class DialogueRateLimitException(DateTimeOffset retryAt)
    : HttpRequestException("请求过于频繁，请稍后再试。", null, HttpStatusCode.TooManyRequests)
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public sealed class DialogueService(HttpClient client)
{
    private static readonly HashSet<string> Emotions = new(StringComparer.Ordinal) { "neutral", "happy", "shy", "tired", "teasing" };
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
        { "none", "shy", "kiss", "roll", "long_jump", "high_jump", "pull_up", "push_up", "street_dance" };

    public async Task<PetReply> SendAsync(DialogueApiOptions options, string apiKey, string persona,
        string userText, string eventType, string actionId, int affection, CancellationToken cancellationToken,
        IReadOnlyList<DialogueTurn>? history = null, IReadOnlyCollection<string>? installedActions = null)
    {
        if (!options.Enabled) throw new InvalidOperationException("请先启用对话 API。");
        if (!options.TryValidate(out var error)) throw new InvalidOperationException(error);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("请先保存 API 密钥。");
        if (string.IsNullOrWhiteSpace(userText)) throw new ArgumentException("请输入消息。", nameof(userText));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var content = userText.Trim();
        if (content.Length > 1000) content = content[..1000];
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "none" };
        if (options.AllowActionSuggestions)
        {
            var installed = installedActions ?? Actions;
            foreach (var id in installed)
                if (Actions.Contains(id)) allowed.Add(id);
        }
        persona = PersonaStore.Render(persona, eventType, actionId, affection, allowed);
        var context = $"当前事件：{eventType}; 当前动作：{actionId}; 好感度：{Math.Clamp(affection, 0, 100)}/100。只输出 JSON：{{\"text\":\"...\",\"emotion\":\"neutral\",\"action\":\"none\"}}。";
        var messages = new List<DialogueTurn>
        {
            new("system", persona.Length <= 8000 ? persona : persona[..8000]),
            new("system", context)
        };
        if (history is not null)
        {
            var recent = new List<DialogueTurn>();
            var length = 0;
            foreach (var turn in history.TakeLast(12).Reverse())
            {
                if (turn.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(turn.Content)) continue;
                var clipped = turn.Content.Length <= 1000 ? turn.Content : turn.Content[..1000];
                if (length + clipped.Length > 4000) break;
                recent.Add(new DialogueTurn(turn.Role, clipped));
                length += clipped.Length;
            }
            recent.Reverse();
            messages.AddRange(recent);
        }
        messages.Add(new DialogueTurn("user", content));
        var body = JsonSerializer.Serialize(new
        {
            model = options.Model,
            temperature = options.Temperature,
            max_tokens = options.MaxOutputTokens,
            messages = messages.Select(turn => new { role = turn.Role, content = turn.Content }).ToArray()
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, options.EndpointUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = response.Headers.RetryAfter;
                var until = retry?.Date ?? DateTimeOffset.Now.Add(retry?.Delta ?? TimeSpan.FromMinutes(5));
                throw new DialogueRateLimitException(until);
            }
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "密钥或权限不正确。",
                HttpStatusCode.NotFound => "接口地址或模型不存在。",
                _ => $"服务返回 HTTP {(int)response.StatusCode}。"
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        // A compatible service should return a small text response. Bound the payload while reading it.
        await using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (bounded.Length + read > 64 * 1024)
                throw new InvalidDataException("服务响应过长，已停止读取。");
            await bounded.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        bounded.Position = 0;
        using var doc = await JsonDocument.ParseAsync(bounded, new JsonDocumentOptions { MaxDepth = 16 }, timeout.Token);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var responseMessage) ||
            !responseMessage.TryGetProperty("content", out var returnedContent) ||
            returnedContent.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("服务响应不符合兼容对话格式。");
        var raw = returnedContent.GetString() ?? "";
        var reply = ParseReply(raw, options.AllowActionSuggestions);
        return allowed.Contains(reply.Action) ? reply : reply with { Action = "none" };
    }

    public static PetReply ParseReply(string raw, bool allowAction)
    {
        var content = raw.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal))
            content = content.Trim('`', '\r', '\n', ' ').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
        string text;
        var emotion = "neutral";
        var action = "none";
        try
        {
            using var parsed = JsonDocument.Parse(content);
            var root = parsed.RootElement;
            if (!root.TryGetProperty("text", out var outputText) || outputText.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("服务没有返回可显示的文字。");
            text = outputText.GetString() ?? "";
            if (root.TryGetProperty("emotion", out var e) && e.ValueKind == JsonValueKind.String) emotion = e.GetString() ?? "neutral";
            if (root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String) action = a.GetString() ?? "none";
        }
        catch (JsonException)
        {
            if (content.StartsWith('{') || content.StartsWith('['))
                throw new InvalidDataException("服务返回了不完整的 JSON。");
            text = content;
        }
        if (!Emotions.Contains(emotion)) emotion = "neutral";
        if (!allowAction || !Actions.Contains(action)) action = "none";
        text = LimitText(text, 80);
        if (text.Length == 0) throw new InvalidDataException("服务没有返回可显示的文字。");
        return new PetReply(text, emotion, action);
    }

    private static string LimitText(string text, int maxElements)
    {
        var clean = new string(text.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();
        var e = StringInfo.GetTextElementEnumerator(clean);
        var result = new StringBuilder();
        for (var i = 0; i < maxElements && e.MoveNext(); i++) result.Append(e.GetTextElement());
        return result.ToString();
    }
}
