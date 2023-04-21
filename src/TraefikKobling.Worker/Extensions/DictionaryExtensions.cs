namespace TraefikKobling.Worker.Extensions;

public static class DictionaryExtensions
{

    public static void Merge<T, K, V>(this T me, IDictionary<K,V> other)
        where T : IDictionary<K, V>
    {
        foreach (var (key, value) in other)
        {
            me[key] = value;
        }
    }
}