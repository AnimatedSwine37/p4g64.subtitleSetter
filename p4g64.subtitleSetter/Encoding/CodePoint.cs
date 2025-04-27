namespace p4g64.subtitleSetter;

/// <summary>
/// From Atlus Script Tools
/// https://github.com/tge-was-taken/Atlus-Script-Tools/blob/0a74cf05de30118ba60e9b1ff7b6cd61677085ed/Source/AtlusScriptLibrary/Common/Text/Encodings/CodePoint.cs
/// </summary>
public struct CodePoint
{
    public byte HighSurrogate;
    public byte LowSurrogate;

    public CodePoint(byte high, byte low)
    {
        HighSurrogate = high;
        LowSurrogate = low;
    }
}