using System.Globalization;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace RoundEndSounds;

public class RoundEndSounds : BasePlugin, IPluginConfig<RoundEndSoundsConfig>
{
    private static DataBaseService? _dataBaseService;
    private static IMenuApi _menuApi = null!;
    private static readonly PluginCapability<IMenuApi?> MenuCapability = new("menu:nfcore");

    private static Timer? _centerHtmlTimer;
    private static bool _gIsCenterHtmlActive;
    private static string _centerSoundName = string.Empty;
    private static string _centerSoundPicture = string.Empty;
    private List<int> _availableSoundIndices = [];
    private int _currentSoundIndex;
    private bool _isCacheLoaded;

    private string _lastSoundPath = "";

    private Dictionary<ulong, UserSettings> _userSettingsCache = new();
    public override string ModuleName => "Round End Sounds";
    public override string ModuleVersion => "v1.0.4";
    public override string ModuleAuthor => "E!N";

    public RoundEndSoundsConfig Config { get; set; } = new();

    public void OnConfigParsed(RoundEndSoundsConfig config)
    {
        Config = config;
        _dataBaseService = new DataBaseService(config);

        Task.Run(async () =>
        {
            await _dataBaseService.InitDataBaseAsync();
            var loadedData = await _dataBaseService.LoadAllUsers();
            _userSettingsCache = loadedData;
            _isCacheLoaded = true;
        });
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnServerPrecacheResources>(OnPrecache);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventRoundMvp>(OnRoundMvp);
        if (Config.MessageType == 2)
            RegisterListener<Listeners.OnTick>(OnTick);

        AddCommand("css_res", "RES main menu", (player, _) => MainMenu(player));
        AddCommand("css_res_reload", "RES config reloading", (_, _) => Config.Reload());
    }

    public override void Unload(bool hotReload)
    {
        RemoveListener<Listeners.OnServerPrecacheResources>(OnPrecache);
        DeregisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        DeregisterEventHandler<EventRoundEnd>(OnRoundEnd);
        DeregisterEventHandler<EventRoundMvp>(OnRoundMvp);
        if (Config.MessageType == 2)
            RemoveListener<Listeners.OnTick>(OnTick);

        RemoveCommand("css_res", (player, _) => MainMenu(player));
        RemoveCommand("css_res_reload", (_, _) => Config.Reload());
    }

    private void OnPrecache(ResourceManifest resource)
    {
        foreach (var file in Config.SoundEventFiles) resource.AddResource("soundevents/" + file);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _menuApi = MenuCapability.Get()!;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot || player.AuthorizedSteamID == null)
            return HookResult.Continue;

        var steamId = player.AuthorizedSteamID.SteamId64;

        if (!_userSettingsCache.ContainsKey(steamId))
            _userSettingsCache[steamId] = new UserSettings
            {
                Volume = Config.DefaultVolume,
                Enabled = true
            };

        PrintLocalizedChat(player, "res.msg");

        return HookResult.Continue;
    }

    private void OnTick()
    {
        if (_centerHtmlTimer == null || !_gIsCenterHtmlActive) return;
        foreach (var p in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine(Localize(p, "res.center_message", _centerSoundName, _centerSoundPicture));
            htmlBuilder.AppendLine("</div>");

            var htmlMessage = htmlBuilder.ToString();
            p.PrintToCenterHtml(htmlMessage);
        }
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (Config.Sounds.Count == 0) return HookResult.Continue;

        KeyValuePair<string, SoundDefinition> selectedSound;

        if (Config.Randomize)
        {
            var index = GetNextRandomIndex();
            selectedSound = Config.Sounds.ElementAt(index);
        }
        else
        {
            if (_currentSoundIndex >= Config.Sounds.Count)
                _currentSoundIndex = 0;

            selectedSound = Config.Sounds.ElementAt(_currentSoundIndex);

            _currentSoundIndex++;
        }

        var soundPath = selectedSound.Value.Sound;
        var soundName = selectedSound.Key;
        var soundPicture = selectedSound.Value.SoundPic;
        _lastSoundPath = soundPath;

        foreach (var player in Utilities.GetPlayers()
                     .Where(player => player is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            if (!GetPlayerResEnabled(player)) continue;

            PlaySound(player, soundPath);

            switch (Config.MessageType)
            {
                case 1:
                    PrintLocalizedChat(player, "res.chat_message", soundName);
                    break;
                case 2:
                    _centerSoundName = soundName;
                    _centerSoundPicture = soundPicture;
                    _gIsCenterHtmlActive = true;
                    _centerHtmlTimer = AddTimer(
                        ConVar.Find("mp_round_restart_delay")?.GetPrimitiveValue<float>() ?? 10f, () =>
                        {
                            _centerHtmlTimer?.Kill();
                            _gIsCenterHtmlActive = false;
                            _centerHtmlTimer = null;
                        });
                    break;
                case 3:
                    CsTeam? winner = Enum.IsDefined(typeof(CsTeam), (byte)@event.Winner) ? (CsTeam)@event.Winner : null;

                    var panel = new EventCsWinPanelRound(false)
                    {
                        FinalEvent = winner is CsTeam.CounterTerrorist
                            ? (int)RoundEndReason.CTsWin
                            : (int)RoundEndReason.TerroristsWin,
                        FunfactToken = Localize(player, "res.win_panel", soundName)
                    };
                    panel.FireEvent(false);
                    break;
            }
        }

        return HookResult.Continue;
    }

    private static HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
    {
        var mvpPlayer = @event.Userid;
        if (mvpPlayer == null)
            return HookResult.Continue;

        mvpPlayer.MVPs = 0;

        return HookResult.Continue;
    }

    private void MainMenu(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.AuthorizedSteamID == null) return;

        var menu = _menuApi.GetMenu(Localize(player, "res.main_menu.title"));

        var isEnabled = GetPlayerResEnabled(player);
        var vol = GetPlayerVolume(player);

        var state = Localize(player, isEnabled ? "res.state.enabled" : "res.state.disabled");
        menu.AddMenuOption(Localize(player, "res.main_menu.state", state), (p, _) =>
        {
            UpdatePlayerSettings(p, vol, !isEnabled);
            MainMenu(p);
        });

        menu.AddMenuOption(Localize(player, "res.main_menu.volume", $"{vol:0.0}"),
            (p, _) => OpenVolumeSettings(p));

        menu.AddMenuOption(Localize(player, "res.main_menu.sounds"), (p, _) => OpenMenuSounds(p));

        menu.AddMenuOption(Localize(player, "res.main_menu.play_last_sound"), (p, _) =>
        {
            if (!string.IsNullOrEmpty(_lastSoundPath))
                PlaySound(p, _lastSoundPath, true);
        });

        menu.Open(player);
    }

    private void OpenVolumeSettings(CCSPlayerController player)
    {
        var currentVol = GetPlayerVolume(player);
        var menu = _menuApi.GetMenu(Localize(player, "res.volume.title", $"{currentVol:0.0}"), MainMenu);
        var isEnabled = GetPlayerResEnabled(player);

        menu.AddMenuOption(Localize(player, "res.volume_up"), (p, _) =>
        {
            var newVol = (float)Math.Round(currentVol + 0.1f, 1);
            if (newVol > 0.9f) newVol = 0.9f;

            UpdatePlayerSettings(p, newVol, isEnabled);
            OpenVolumeSettings(p);
        }, currentVol >= 0.9f);

        menu.AddMenuOption(Localize(player, "res.volume_down"), (p, _) =>
        {
            var newVol = (float)Math.Round(currentVol - 0.1f, 1);
            if (newVol < 0.0f) newVol = 0.0f;

            UpdatePlayerSettings(p, newVol, isEnabled);
            OpenVolumeSettings(p);
        }, currentVol <= 0.0f);

        menu.Open(player);
    }

    private void OpenMenuSounds(CCSPlayerController player)
    {
        var menu = _menuApi.GetMenu(Localize(player, "res.menu_sounds.title"), MainMenu);

        foreach (var (soundName, value) in Config.Sounds)
        {
            var soundPath = value.Sound;

            menu.AddMenuOption(soundName, (p, _) =>
            {
                PlaySound(p, soundPath, true);
                PrintLocalizedChat(p, "res.playing_preview", soundName);
            });
        }

        menu.Open(player);
    }

    private void PlaySound(CCSPlayerController player, string soundPath, bool forcePlay = false)
    {
        if (!_isCacheLoaded) return;
        if (string.IsNullOrEmpty(soundPath)) return;

        if (!forcePlay && !GetPlayerResEnabled(player)) return;

        var volume = GetPlayerVolume(player);

        if (volume <= 0.01f) return;

        var outputVolume = Math.Clamp(volume * volume, 0.0f, 1.0f);

        if (soundPath.StartsWith("sounds/"))
        {
            var volumeArgument = outputVolume.ToString("0.00", CultureInfo.InvariantCulture);
            player.ReplicateConVar("snd_toolvolume", volumeArgument);
            player.ExecuteClientCommand($"play {soundPath}");
        }
        else
            player.EmitSound(soundPath, player, outputVolume);
    }

    private void PrintLocalizedChat(CCSPlayerController player, string key, params object[] args)
    {
        var message = Localize(player, key, args);
        if (!string.IsNullOrEmpty(message))
            player.PrintToChat($" {message}");
    }

    private string Localize(CCSPlayerController? player, string key, params object[] args)
    {
        CultureInfo[] cultures =
        [
            player.GetLanguage(),
            CultureInfo.GetCultureInfo("en"),
            CultureInfo.GetCultureInfo("ru")
        ];

        foreach (var culture in cultures.DistinctBy(candidate => candidate.Name))
        {
            using var temporaryCulture = new WithTemporaryCulture(culture);
            var localized = Localizer[key, args];
            if (!localized.ResourceNotFound)
                return localized.Value;
        }

        Logger.LogWarning("Missing localization key {LocalizationKey} for all configured fallback languages", key);
        return string.Empty;
    }

    private float GetPlayerVolume(CCSPlayerController player)
    {
        if (player.AuthorizedSteamID == null) return Config.DefaultVolume;

        return _userSettingsCache.TryGetValue(player.AuthorizedSteamID.SteamId64, out var settings)
            ? settings.Volume
            : Config.DefaultVolume;
    }

    private bool GetPlayerResEnabled(CCSPlayerController player)
    {
        if (player.AuthorizedSteamID == null) return true;

        return !_userSettingsCache.TryGetValue(player.AuthorizedSteamID.SteamId64, out var settings) ||
               settings.Enabled;
    }

    private void UpdatePlayerSettings(CCSPlayerController player, float volume, bool enabled)
    {
        if (player.AuthorizedSteamID == null) return;
        var steamId = player.AuthorizedSteamID.SteamId64;

        var newSettings = new UserSettings { Volume = volume, Enabled = enabled };

        _userSettingsCache[steamId] = newSettings;

        Task.Run(() => _dataBaseService?.SaveUser(steamId, newSettings));
    }

    private int GetNextRandomIndex()
    {
        if (_availableSoundIndices.Count == 0 || _availableSoundIndices.Any(x => x >= Config.Sounds.Count))
            _availableSoundIndices = Enumerable.Range(0, Config.Sounds.Count).ToList();

        var randomListIndex = new Random().Next(0, _availableSoundIndices.Count);

        var actualSoundIndex = _availableSoundIndices[randomListIndex];

        _availableSoundIndices.RemoveAt(randomListIndex);

        return actualSoundIndex;
    }
}

public class UserSettings
{
    public float Volume { get; init; }
    public bool Enabled { get; init; }
}
