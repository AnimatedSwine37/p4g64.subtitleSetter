using System.Runtime.InteropServices;
using System.Text;
using Reloaded.Memory;
using SubtitlesParser.Classes;
using static p4g64.subtitleSetter.Utils;

namespace p4g64.subtitleSetter;

public unsafe class Subtitles : IDisposable
{
    public List<SubtitleItem> SubtitleItems { get; }
    public Encoding SubtitleEncoding { get; }
    private NativeSubtitles* _nativeSubtitles = (NativeSubtitles*)IntPtr.Zero;

    public Subtitles(List<SubtitleItem> subtitleItems, Encoding subtitleEncoding)
    {
        SubtitleItems = subtitleItems;
        SubtitleEncoding = subtitleEncoding;
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
        var memory = Memory.Instance;
        _nativeSubtitles =
            (NativeSubtitles*)memory.Allocate((nuint)(sizeof(NativeSubtitles) * (SubtitleItems.Count + 1))).Address;
        LogDebug(
            $"Writing native subtitles to 0x{(nuint)_nativeSubtitles:X} ({sizeof(NativeSubtitles) * (SubtitleItems.Count + 1)} bytes)");

        // Write subtitles
        for (var i = 0; i < SubtitleItems.Count; i++)
        {
            var subtitle = SubtitleItems[i];
            var text = string.Join("\n", subtitle.Lines);
            byte[] textBytes;
            
            // Try catch since we can't let the game crash if we fail this! Will happen if characters are missing from encoding
            try
            {
                textBytes = SubtitleEncoding.GetBytes(text);
            }
            catch (Exception exception)
            {
                LogError($"Failed to encode subtitle text '{text}'.", exception);
                text = "ERROR";
                textBytes = Encoding.ASCII.GetBytes(text); // Assuming ASCII *should* always work to some degree
            }

            var textPtr = (char*)memory.Allocate((nuint)textBytes.Length).Address;
            memory.WriteRaw((nuint)textPtr, textBytes);
            LogDebug($"Wrote subtitles text to 0x{(nuint)textPtr:X}. Text: {text}");

            var nativeSubtitle = &_nativeSubtitles[i];
            nativeSubtitle->Text = textPtr;
            nativeSubtitle->StartTime =
                subtitle.StartTime / 1000.0; // StartTime is in milliseconds, we need seconds as double
            nativeSubtitle->EndTime =
                subtitle.EndTime / 1000.0; // EndTime is in milliseconds, we need seconds as double
        }

        // Write an extra block at the end, required to indicate we're done (and prevent the game from crashing)
        var endSubtitle = &_nativeSubtitles[SubtitleItems.Count];
        endSubtitle->Text = null;
        endSubtitle->StartTime = 0;
        endSubtitle->EndTime = 0;
    }

    public void Dispose()
    {
        for (int i = 0; i < SubtitleItems.Count; i++)
        {
            // In case the subtitle text failed to be written, the pointer will be null
            if (_nativeSubtitles[i].Text != null)
            {
                Marshal.FreeHGlobal((IntPtr)_nativeSubtitles[i].Text);
            }
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