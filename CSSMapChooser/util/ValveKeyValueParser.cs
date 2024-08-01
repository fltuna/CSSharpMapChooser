namespace CSSMapChooser;

public class ValveKeyValueParser
{
    private readonly string _content;
    private int _position;

    public ValveKeyValueParser(string content)
    {
        _content = content;
        _position = 0;
    }

    public Dictionary<string, MapData> Parse()
    {
        Dictionary<string, MapData> result = new Dictionary<string, MapData>();
        Dictionary<string, object> root = ParseObject();

        if (root.TryGetValue("MapList", out var mapList) && mapList is Dictionary<string, object> mapListDict)
        {
            foreach (var kvp in mapListDict)
            {
                if (kvp.Value is Dictionary<string, object> mapProperties)
                {
                    var properties = new Dictionary<string, string>();
                    foreach (var prop in mapProperties)
                    {
                        properties[prop.Key] = prop.Value.ToString() ?? "";
                    }
                    result[kvp.Key] = new MapData(kvp.Key, new MapProperties(properties));
                }
            }
        }

        return result;
    }

    private Dictionary<string, object> ParseObject()
    {
        var result = new Dictionary<string, object>();
        SkipWhitespace();

        while (_position < _content.Length && _content[_position] != '}')
        {
            var key = ParseString();
            SkipWhitespace();

            if (_position < _content.Length && _content[_position] == '{')
            {
                _position++; // Skip '{'
                result[key] = ParseObject();
                SkipWhitespace();
                if (_position < _content.Length && _content[_position] == '}')
                {
                    _position++; // Skip '}'
                }
            }
            else
            {
                var value = ParseString();
                result[key] = value;
            }

            SkipWhitespace();
        }

        if (_position < _content.Length && _content[_position] == '}')
        {
            _position++; // Skip '}'
        }

        return result;
    }

    private string ParseString()
    {
        SkipWhitespace();
        var start = _position;

        if (_content[_position] == '"')
        {
            start++;
            _position++;
            while (_position < _content.Length && _content[_position] != '"')
            {
                _position++;
            }
            _position++; // Skip closing quote
            return _content.Substring(start, _position - start - 1);
        }
        else
        {
            while (_position < _content.Length && !char.IsWhiteSpace(_content[_position]) && _content[_position] != '{' && _content[_position] != '}')
            {
                _position++;
            }
            return _content.Substring(start, _position - start);
        }
    }

    private void SkipWhitespace()
    {
        while (_position < _content.Length && char.IsWhiteSpace(_content[_position]))
        {
            _position++;
        }
    }
}