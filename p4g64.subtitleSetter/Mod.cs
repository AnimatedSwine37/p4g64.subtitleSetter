using System.Diagnostics;
using Reloaded.Mod.Interfaces;
using p4g64.subtitleSetter.Template;
using p4g64.subtitleSetter.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using p4g64.lib.interfaces;
using p4g64.lib.interfaces.Meta;
using Reloaded.Mod.Interfaces.Internal;
using SubtitlesParser.Classes.Parsers;
using static p4g64.subtitleSetter.Utils;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p4g64.subtitleSetter;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private IP4GLib _p4gLib;
    private SubtitleHook _subtitleHook;

    private static Dictionary<string, Language> _languageCodeToId = new()
    {
        ["jp"] = Language.Japanese,
        ["en"] = Language.English,
        ["ko"] = Language.Korean,
        ["zh-hant"] = Language.TraditionalChinese,
        ["zh-hans"] = Language.SimplifiedChinese,
        ["fr"] = Language.French,
        ["it"] = Language.Italian,
        ["de"] = Language.German,
        ["es"] = Language.Spanish,
    };

    private static Dictionary<Language, Encoding> _languageEncodings = new();

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

        if (!Utils.Initialise(_logger, _configuration, _modLoader))
        {
            return;
        }

        var p4gLibController = _modLoader.GetController<IP4GLib>();
        if (p4gLibController == null || !p4gLibController.TryGetTarget(out _p4gLib))
        {
            LogError("Unable to get controller for P4G Lib, stuff won't work :(");
            return;
        }

        if (_hooks == null)
        {
            LogError("Failed to get Reloaded Hooks, nothing will work!");
            return;
        }

        SetupEncodings();

        _subtitleHook = new SubtitleHook(_hooks, _p4gLib);
        _modLoader.ModLoaded += ModLoaded;
    }

    private void SetupEncodings()
    {
        var modDir = _modLoader.GetDirectoryForModId(_modConfig.ModId);
        AtlusEncoding.SetCharsetDirectory(Path.Combine(modDir, "Charsets"));

        _languageEncodings.Add(Language.Japanese, AtlusEncoding.Create("P4G_JP"));
        _languageEncodings.Add(Language.English, AtlusEncoding.Create("P4G_EFIGS"));
        _languageEncodings.Add(Language.French, AtlusEncoding.Create("P4G_EFIGS"));
        _languageEncodings.Add(Language.Italian, AtlusEncoding.Create("P4G_EFIGS"));
        _languageEncodings.Add(Language.German, AtlusEncoding.Create("P4G_EFIGS"));
        _languageEncodings.Add(Language.Spanish, AtlusEncoding.Create("P4G_EFIGS"));
        _languageEncodings.Add(Language.Korean, AtlusEncoding.Create("P4G_Korean"));
        _languageEncodings.Add(Language.SimplifiedChinese, AtlusEncoding.Create("P4G_CHS"));
        _languageEncodings.Add(Language.TraditionalChinese, AtlusEncoding.Create("P4G_CHT"));
    }

    private void ModLoaded(IModV1 mod, IModConfigV1 modConfig)
    {
        if (modConfig.ModDependencies.Contains(_modConfig.ModId))
        {
            var modDir = _modLoader.GetDirectoryForModId(modConfig.ModId);
            var subtitlesDir = Path.Combine(modDir, "Subtitles");
            if (Directory.Exists(subtitlesDir))
            {
                LoadSubtitlesFromFolder(subtitlesDir);
            }
            else
            {
                LogWarn(
                    $"No subtitles found for mod {modConfig.ModId}. Expected folder '{subtitlesDir}' was not found.");
            }
        }
    }


    private void LoadSubtitlesFromFolder(string folder)
    {
        Log($"Loading subtitles from '{folder}'");

        foreach (var dir in Directory.GetDirectories(folder))
        {
            var dirName = Path.GetFileName(dir);
            if (!_languageCodeToId.TryGetValue(dirName.ToLower(), out var language))
            {
                LogError($"Unable to determine language for subtitles in '{dir}', ignoring them." +
                         $"\nExpected direction name to be one of {string.Join(", ", _languageCodeToId.Keys)}.");
                continue;
            }

            var encoding = _languageEncodings[language];
            if (File.Exists(Path.Combine(dir, "Charset.tsv")))
            {
                Log($"Using custom charset for subtitles in directory '{dir}'");
                encoding = new AtlusEncoding("CustomSubtitleEncoding", Path.Combine(dir, "Charset.tsv"));
            }

            foreach (var file in Directory.GetFiles(dir, "*.srt", SearchOption.AllDirectories))
            {
                LoadSubtitleFile(file, language, encoding);
            }
        }
    }

    private void LoadSubtitleFile(string filePath, Language language, Encoding subtitleEncoding)
    {
        var fileName = Path.GetFileName(filePath);
        int id;
        var nameIdMatch = Regex.Match(fileName, @"P4CT(\d{3})\.srt");
        if (nameIdMatch.Success)
        {
            id = int.Parse(nameIdMatch.Value) - 1;
        }
        else if (fileName.Equals("P4CTOP3_E.srt") || fileName.Equals("P4CTOP3.srt"))
        {
            // Same id used for both, the loaded one depends on in game language
            id = 43;
        }
        else if (fileName.Equals("P4CTOP1.srt"))
        {
            id = 21;
        }
        else
        {
            LogError($"Unable to determine movie id for file '{fileName}', ignoring it.");
            return;
        }

        var parser = new SrtParser();
        using var fileStream = File.OpenRead(filePath);
        var items = parser.ParseStream(fileStream, Encoding.Default);
        if (items == null)
        {
            LogError($"Failed to parse Subtitle file {filePath}, ignoring it.");
            return;
        }

        _subtitleHook.RegisterSubtitles(id, language, new Subtitles(items, subtitleEncoding));
        Log($"Added subtitles for movie {id} in {language} from {fileName}");
    }


    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod()
    {
    }
#pragma warning restore CS8618

    #endregion
}