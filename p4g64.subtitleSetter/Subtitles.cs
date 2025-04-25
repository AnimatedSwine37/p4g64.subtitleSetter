using System.Runtime.InteropServices;
using SubtitlesParser.Classes;
using static p4g64.subtitleSetter.Utils;

namespace p4g64.subtitleSetter;

public unsafe class Subtitles: IDisposable
{
    public List<SubtitleItem> SubtitleItems { get; }
    private NativeSubtitles* _nativeSubtitles = (NativeSubtitles*)IntPtr.Zero;

    public Subtitles(List<SubtitleItem> subtitleItems)
    {
        SubtitleItems = subtitleItems;
    }

    public NativeSubtitles* GetNativeSubtitles()
    {
        if (_nativeSubtitles != (NativeSubtitles*)IntPtr.Zero)
        {
            return _nativeSubtitles;
        }
        
        WriteNativeSubtitles();
        return _nativeSubtitles;
    }

    private void WriteNativeSubtitles()
    {
        _nativeSubtitles = (NativeSubtitles*)Marshal.AllocHGlobal(sizeof(NativeSubtitles) * SubtitleItems.Count);
        LogDebug($"Writing native subtitles to 0x{(nuint)_nativeSubtitles:X} ({sizeof(NativeSubtitles) * SubtitleItems.Count} bytes)");

        for (var i = 0; i < SubtitleItems.Count; i++)
        {
            var subtitle = SubtitleItems[i];
            var text = string.Join("\n", subtitle.Lines);
            var textPtr = (char*)Marshal.StringToHGlobalAnsi(text);
            LogDebug($"Wrote subtitles text to 0x{(nuint)textPtr:X}. Text: {text}");

            var nativeSubtitle = &_nativeSubtitles[i];
            nativeSubtitle->Text = textPtr;
            nativeSubtitle->StartTime = subtitle.StartTime / 1000.0; // StartTime is in milliseconds, we need seconds as double
            nativeSubtitle->EndTime = subtitle.EndTime / 1000.0; // EndTime is in milliseconds, we need seconds as double
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < SubtitleItems.Count; i++)
        {
            Marshal.FreeHGlobal((IntPtr)_nativeSubtitles[i].Text);
        }
        Marshal.FreeHGlobal((IntPtr)_nativeSubtitles);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSubtitles
    {
        public double StartTime;
        public double EndTime;
        public char* Text;
    }
}