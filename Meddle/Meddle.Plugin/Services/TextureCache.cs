using Dalamud.Interface.Textures.TextureWraps;
using Microsoft.Extensions.Logging;

namespace Meddle.Plugin.Services;

public class CachedTexture : IDisposable
{
    public CachedTexture(IDalamudTextureWrap wrap)
    {
        Wrap = wrap;
        LastAccessTime = DateTime.Now;
    }

    public IDalamudTextureWrap Wrap { get; set; }
    public DateTime LastAccessTime { get; set; }

    public void Dispose()
    {
        Wrap.Dispose();
    }
}

public sealed class TextureCache : IDisposable, IService
{
    // GetOrAdd runs on the framework thread, the cleanup timer on the thread pool: every cache access holds cacheLock.
    // Wrap.Dispose only enqueues a deferred release (thread-safe), so it is called outside the lock from either thread.
    private readonly Dictionary<string, CachedTexture> cache = new();
    private readonly object cacheLock = new();
    private readonly Timer cleanupTimer;
    private readonly TimeSpan expirationTime;
    private readonly ILogger<TextureCache> logger;
    private volatile bool disposed;
    private int cleanupFailureLogged;

    public TextureCache(ILogger<TextureCache> logger)
    {
        expirationTime = TimeSpan.FromSeconds(10);
        this.logger = logger;
        cleanupTimer = new Timer(CleanupExpiredTextures, null, expirationTime, expirationTime);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IDalamudTextureWrap GetOrAdd(string key, Func<IDalamudTextureWrap> createWrap)
    {
        if (TryGetCached(key, out var cached))
            return cached;

        // Created outside the lock: building the bitmap is slow and must not stall the cleanup thread.
        var wrap = createWrap();
        Store(key, wrap);
        return wrap;
    }

    public async Task<IDalamudTextureWrap> GetOrAddAsync(string key, Func<Task<IDalamudTextureWrap>> createWrap)
    {
        if (TryGetCached(key, out var cached))
            return cached;

        var wrap = await createWrap();
        Store(key, wrap);
        return wrap;
    }

    private bool TryGetCached(string key, out IDalamudTextureWrap wrap)
    {
        lock (cacheLock)
        {
            if (cache.TryGetValue(key, out var cachedTexture))
            {
                cachedTexture.LastAccessTime = DateTime.Now;
                wrap = cachedTexture.Wrap;
                return true;
            }
        }

        wrap = null!;
        return false;
    }

    private void Store(string key, IDalamudTextureWrap wrap)
    {
        CachedTexture? replaced;
        lock (cacheLock)
        {
            cache.Remove(key, out replaced);
            cache[key] = new CachedTexture(wrap);
        }

        if (replaced != null && !ReferenceEquals(replaced.Wrap, wrap))
            replaced.Dispose();
    }

    // Timer callback on the thread pool: an escaping exception would end the game process.
    private void CleanupExpiredTextures(object? state)
    {
        try
        {
            if (disposed)
                return;

            var now = DateTime.Now;
            List<KeyValuePair<string, CachedTexture>>? expired = null;
            lock (cacheLock)
            {
                foreach (var kvp in cache)
                {
                    if (now - kvp.Value.LastAccessTime > expirationTime)
                        (expired ??= new()).Add(kvp);
                }

                if (expired != null)
                {
                    foreach (var kvp in expired)
                        cache.Remove(kvp.Key);
                }
            }

            if (expired == null)
                return;

            foreach (var kvp in expired)
            {
                logger.LogDebug("Removing expired texture: {Key}", kvp.Key);
                kvp.Value.Dispose();
            }
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref cleanupFailureLogged, 1) == 0)
                logger.LogWarning("Texture cache cleanup failed ({ExceptionType}); later failures are not logged.", ex.GetType().Name);
        }
    }

    private void ReleaseUnmanagedResources()
    {
        List<CachedTexture> all;
        lock (cacheLock)
        {
            all = cache.Values.ToList();
            cache.Clear();
        }

        foreach (var cachedTexture in all)
        {
            cachedTexture.Dispose();
        }
    }

    private void Dispose(bool disposing)
    {
        disposed = true;
        if (disposing)
        {
            cleanupTimer.Dispose();
        }
        ReleaseUnmanagedResources();
    }

    ~TextureCache()
    {
        Dispose(false);
    }
}
