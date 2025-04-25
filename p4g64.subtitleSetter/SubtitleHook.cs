using p4g64.lib.interfaces;
using p4g64.lib.interfaces.Meta;
using Reloaded.Hooks.Definitions;
using static p4g64.subtitleSetter.Utils;

namespace p4g64.subtitleSetter;

public unsafe class SubtitleHook
{
    private IP4GLib _p4gLib;
    private Dictionary<Language, Dictionary<int, Subtitles>> _subtitles = new();
    
    private IHook<LoadSubtitlesDelegate>? _loadSubtitlesHook;
    private SetCurrentSubtitleText _setCurrentSubtitleText;
    
    private Subtitles.NativeSubtitles** _currentSubtitles;
    private nuint* _subtitleDrawFunc;
    
    internal SubtitleHook(IReloadedHooks hooks, IP4GLib p4gLib)
    {
        _p4gLib = p4gLib;
        
        SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 63 D9 48 89 D7", "LoadSubtitles",
            address =>
            {
                _loadSubtitlesHook = hooks.CreateHook<LoadSubtitlesDelegate>(LoadSubtitles, address).Activate();
            });
        
        SigScan("40 55 48 83 EC 20 48 89 5C 24 ?? 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ??", "SetCurrentSubtitleText",
            address =>
            {
                _setCurrentSubtitleText = hooks.CreateWrapper<SetCurrentSubtitleText>(address, out _);
            });
        
        SigScan("48 89 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 49 ??", "CurrentSubtitlesPtr", address =>
        {
            _currentSubtitles = (Subtitles.NativeSubtitles**)GetGlobalAddress(address + 3);
            LogDebug($"Found CurrentSubtitles at 0x{(nuint)_currentSubtitles:X}");

            _subtitleDrawFunc =(nuint*)GetGlobalAddress(address + 19);
            LogDebug($"Found SubtitleDrawFunc at 0x{(nuint)_subtitleDrawFunc:X}");
        });
    }

    internal void RegisterSubtitles(int movieId, Language language, Subtitles subtitles)
    {
        if (!_subtitles.TryGetValue(language, out var langSubtitles))
        {
            langSubtitles = new Dictionary<int, Subtitles>();
            _subtitles.Add(language, langSubtitles);
        }

        langSubtitles[movieId] = subtitles;
    }
    
    private void LoadSubtitles(int movieId, nuint drawFunc)
    {
        var language = _p4gLib.MetaController.GetLanguage();
        LogDebug($"Loading subtitles for movie {movieId} with language {language}");
        
        if (_subtitles.TryGetValue(language, out var langSubtitles) &&
            langSubtitles.TryGetValue(movieId, out var movieSubtitle))
        {
            Log($"Replacing subtitles for movie {movieId}");
            var nativeSubtitle = movieSubtitle.GetNativeSubtitles();
            *_subtitleDrawFunc = drawFunc;
            *_currentSubtitles = nativeSubtitle;
            _setCurrentSubtitleText(nativeSubtitle->Text);
        }
        else
        {
            LogDebug($"Using original function for movie {movieId}");
            _loadSubtitlesHook!.OriginalFunction(movieId, drawFunc);
        }
    }

    private delegate void LoadSubtitlesDelegate(int movieId, nuint drawFunc);
    private delegate void SetCurrentSubtitleText(char* text);
}