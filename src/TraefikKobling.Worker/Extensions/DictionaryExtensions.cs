namespace TraefikKobling.Worker.Extensions;

public static class DictionaryExtensions
{
    public static void Merge<T, TKey, TValue>(this T me, IDictionary<TKey,TValue> other)
        where T : IDictionary<TKey, TValue>
    {
        foreach (var (key, value) in other)
        {
            me[key] = value;
        }
    }

    public static bool SetEquals(this IDictionary<string,string> dictionary, IDictionary<string,string> other)
    {
        var comparer = new KeyValueComparer();
        return dictionary.ToHashSet(comparer).SetEquals(other.ToHashSet(comparer));
    }

    private class KeyValueComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        private static readonly StringComparer Comparer = StringComparer.Ordinal;
        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return Comparer.Equals(x.Key, y.Key) && Comparer.Equals(x.Value, y.Value);
        }

        public int GetHashCode(KeyValuePair<string, string> obj) => obj.Key.GetHashCode() ^ obj.Value.GetHashCode();
    }
}