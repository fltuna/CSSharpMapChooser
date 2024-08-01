namespace CSSMapChooser;

public static class MapDataParser
{
    public static Dictionary<string, MapData> ParseConfig(string filePath)
    {
        string content = File.ReadAllText(filePath);
        ValveKeyValueParser parser = new ValveKeyValueParser(content);
        return parser.Parse();
    }
}