using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DependencyUpdated.Projects.DotNet.UnitTests;

internal sealed class MockMemoryCache : IMemoryCache
{
    private Dictionary<object, MockEntry> Cache { get; } = new();
    
    public void Dispose()
    {
    }

    public bool TryGetValue(object key, out object? value)
    {
        var exists = Cache.TryGetValue(key, out var dictValue);
        value = dictValue?.Value;
        return exists;
    }

    public ICacheEntry CreateEntry(object key)
    {
        return new MockEntry() { Key = key };
    }

    public void AddEntry(object key, object value)
    {
        Cache.Add(key, new MockEntry() { Key = key, Value = value });
    }

    public void Remove(object key)
    {
        Cache.Remove(key);
    }
}

internal sealed class MockEntry : ICacheEntry
{
    public void Dispose()
    {
    }

    public object Key { get; init; }
    
    public object? Value { get; set; }
    
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    
    public TimeSpan? SlidingExpiration { get; set; }
    
    public IList<IChangeToken> ExpirationTokens { get; }
    
    public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; }
    
    public CacheItemPriority Priority { get; set; }
 
    public long? Size { get; set; }
}