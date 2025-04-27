using System.Text;

namespace p4g64.subtitleSetter;

/// <summary>
/// From Atlus Script Tools
/// https://github.com/tge-was-taken/Atlus-Script-Tools/blob/0a74cf05de30118ba60e9b1ff7b6cd61677085ed/Source/AtlusScriptLibrary/Common/Text/Encodings/UnsupportedCharacterException.cs
/// </summary>
public class UnsupportedCharacterException : Exception
{
    public string EncodingName { get; }

    public string Character { get; }

    public UnsupportedCharacterException(string encodingName, string c)
        : base($"Encoding {encodingName} does not support character: {c} ({EncodeNonAsciiCharacters(c)})")
    {
        EncodingName = encodingName;
        Character = c;
    }

    static string EncodeNonAsciiCharacters(string value)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in value)
        {
            if (c > 127)
            {
                // This character is too big for ASCII
                string encodedValue = "\\u" + ((int)c).ToString("x4");
                sb.Append(encodedValue);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}