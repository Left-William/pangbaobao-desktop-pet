using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PangBaoBaoPet;

/// <summary>Bounded cache for decoded PNG frames. Frames stay immutable across UI and prefetch threads.</summary>
public sealed class FrameCache
{
    private const long BudgetBytes = 128L * 1024 * 1024;
    private const int MaxPendingPrefetch = 2;
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<Entry> _recent = new();
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private long _bytes;
    private int _pendingCount;

    private sealed record Entry(string Path, BitmapImage Image, long Bytes);

    public BitmapImage Get(string path)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var cached))
            {
                _recent.Remove(cached);
                _recent.AddFirst(cached);
                return cached.Value.Image;
            }
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        var bytes = (long)image.PixelWidth * image.PixelHeight * 4;

        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var existing))
            {
                _recent.Remove(existing);
                _recent.AddFirst(existing);
                return existing.Value.Image;
            }
            var node = _recent.AddFirst(new Entry(path, image, bytes));
            _entries.Add(path, node);
            _bytes += bytes;
            while (_bytes > BudgetBytes && _recent.Count > 1)
            {
                var victim = _recent.Last!;
                _bytes -= victim.Value.Bytes;
                _entries.Remove(victim.Value.Path);
                _recent.RemoveLast();
            }
            return image;
        }
    }

    public void Prefetch(string path)
    {
        lock (_gate)
        {
            if (_entries.ContainsKey(path) || _pendingCount >= MaxPendingPrefetch || !_pending.Add(path)) return;
            _pendingCount++;
        }
        _ = Task.Run(() =>
        {
            try { Get(path); }
            catch (Exception) { /* The visible frame load reports the error when needed. */ }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(path);
                    _pendingCount--;
                }
            }
        });
    }
}
