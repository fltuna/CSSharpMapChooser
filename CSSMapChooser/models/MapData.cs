namespace CSSMapChooser;

public class MapData
{
    public string MapName { get; }
    public MapProperties MapProperties { get; }

    public MapData(string mapName, MapProperties properties)
    {
        MapName = mapName;
        MapProperties = properties;
    }
}