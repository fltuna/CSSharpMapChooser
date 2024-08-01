namespace CSSMapChooser;

public class MapProperties
{
    public Dictionary<string, string> Properties { get; }

    public MapProperties(Dictionary<string, string> properties)
    {
        Properties = properties;
    }

    public T? GetValue<T>(string key)
    {
        if (!Properties.TryGetValue(key, out var value))
        {
            return default;
        }

        return (T)Convert.ChangeType(value, typeof(T))!;
    }
}