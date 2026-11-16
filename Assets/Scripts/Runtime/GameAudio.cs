using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Plays the named audio cues through Unity AudioSources.</summary>
public sealed class GameAudio : MonoBehaviour
{
    internal enum Channel { Music, Sound, Ambient, Footsteps }

    private static GameAudio instance;
    private readonly Dictionary<string, AudioClip> cues = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> wiredButtons = new();
    private AudioSource music;
    private AudioSource ambience;
    private AudioSource effects;
    private AudioSource footsteps;
    private float nextButtonScan;
    private float nextMusicRefresh;
    private string currentMusic;
    private string currentAmbience;
    private int lastMenuClickFrame = -1;
    private int morningSongDay;
    private string lastLocationName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null) new GameObject("Game Audio").AddComponent<GameAudio>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        music = MakeSource("Music", 0.55f, true);
        ambience = MakeSource("Ambience", 0.22f, true);
        effects = MakeSource("Effects", 0.85f, false);
        footsteps = MakeSource("Footsteps", 0.85f, false);
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("GameMenu.Volume", 1f));
        foreach (Channel channel in Enum.GetValues(typeof(Channel)))
            ApplyChannelVolume(channel, GetChannelVolume(channel));
        var library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
        if (library == null)
            Debug.LogWarning("[GameAudio] Cue library missing. Open Unity or run Tools > Audio > Rebuild Cue Library.");
        else foreach (var entry in library.entries)
            if (entry.clip != null && !string.IsNullOrEmpty(entry.cue)) cues[entry.cue] = entry.clip;
        foreach (string cue in new[] { "bigDeSelect", "cancel", "FishHit", "fishEscape",
                     "openChest", "swordswipe", "clubswipe" })
            if (cues.TryGetValue(cue, out AudioClip clip) && clip.length > 10f)
                Debug.LogWarning($"[GameAudio] {cue}.wav is {clip.length:0.0}s; this short " +
                    "sound effect appears to be an incorrect export. Replace its WAV or soundbank cue.");
        foreach (string cue in new[] { "MainTheme", "spring1" })
            if (cues.TryGetValue(cue, out AudioClip clip) && clip.length < 10f)
                Debug.LogWarning($"[GameAudio] {cue}.wav is only {clip.length:0.0}s; " +
                    "the music export appears truncated. Replace its WAV or soundbank cue.");
        foreach (string cue in new[] { "smallSelect", "bigSelect", "button1", "boop", "springtown" })
            if (cues.TryGetValue(cue, out AudioClip clip)) clip.LoadAudioData();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private AudioSource MakeSource(string sourceName, float volume, bool loop)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = volume;
        return source;
    }

    internal static float GetChannelVolume(Channel channel) =>
        Mathf.Clamp01(PlayerPrefs.GetFloat("GameMenu.Volume." + channel, 1f));

    internal static void SetChannelVolume(Channel channel, float volume)
    {
        PlayerPrefs.SetFloat("GameMenu.Volume." + channel, volume);
        if (instance != null) instance.ApplyChannelVolume(channel, volume);
    }

    private void ApplyChannelVolume(Channel channel, float volume)
    {
        switch (channel)
        {
            case Channel.Music: music.volume = 0.55f * volume; break;
            case Channel.Sound: effects.volume = 0.85f * volume; break;
            case Channel.Ambient: ambience.volume = 0.22f * volume; break;
            case Channel.Footsteps: footsteps.volume = 0.85f * volume; break;
        }
    }

    internal static void PlayFootstep(string cue)
    {
        if (TryGet(cue, out AudioClip clip)) instance.footsteps.PlayOneShot(clip, 0.35f);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        wiredButtons.Clear();
        WireButtons();
        if (scene.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PlayMusic("MainTheme");
            PlayAmbience(null);
        }
        else if (!StartupSession.IsOpening)
        {
            RefreshLocationMusic();
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextButtonScan)
        {
            nextButtonScan = Time.unscaledTime + 0.75f;
            WireButtons();
        }
        if (Time.unscaledTime >= nextMusicRefresh &&
            SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0 &&
            FarmDayClock.Instance?.IsCutscenePaused != true)
        {
            nextMusicRefresh = Time.unscaledTime + 5f;
            RefreshLocationMusic();
        }
    }

    private void WireButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!wiredButtons.Add(button.GetInstanceID())) continue;
            Button captured = button;
            captured.onClick.AddListener(() => Play(ButtonCue(captured)));
        }
    }

    private static string ButtonCue(Button button)
    {
        string name = button.name;
        // New/Load/Co-op -> select, Exit -> bigDeSelect.
        if (name is "ButtonNew" or "ButtonLoad" or "ButtonCoop") return "select";
        if (name == "ButtonExit") return "bigDeSelect";

        if (button.GetComponentInParent<StartupMenuFlow>() != null)
        {
            if (name.StartsWith("Hair", StringComparison.Ordinal) &&
                (name.EndsWith("Prev", StringComparison.Ordinal) || name.EndsWith("Next", StringComparison.Ordinal)))
                return "grassyStep";
            if (name.StartsWith("Skin", StringComparison.Ordinal) &&
                (name.EndsWith("Prev", StringComparison.Ordinal) || name.EndsWith("Next", StringComparison.Ordinal)))
                return "skeletonStep";
            if (name.StartsWith("Accessory", StringComparison.Ordinal) &&
                (name.EndsWith("Prev", StringComparison.Ordinal) || name.EndsWith("Next", StringComparison.Ordinal)))
                return "purchase";
            if (name.StartsWith("Direction", StringComparison.Ordinal)) return "pickUpItem";
            if (name == "Randomize") return "drumkit6";
            if (name.EndsWith("Prev", StringComparison.Ordinal) || name.EndsWith("Next", StringComparison.Ordinal))
                return "coin";
            if (name is "Back" or "Done") return "bigDeSelect";
            if (name == "Create") return "smallSelect";
            return "select";
        }
        if (name.Contains("Back", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Close", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Exit", StringComparison.OrdinalIgnoreCase)) return "bigDeSelect";
        if (name.Contains("Tab", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Prev", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Next", StringComparison.OrdinalIgnoreCase)) return "smallSelect";
        return "select";
    }

    /// <summary>The clip behind a cue, for sounds that need their own looping source or pitch.</summary>
    internal static AudioClip Clip(string cue) => TryGet(cue, out AudioClip clip) ? clip : null;

    /// <summary>Current sound-effect volume (master and channel) for private audio sources.</summary>
    internal static float SoundVolume => 0.85f * GetChannelVolume(Channel.Sound);

    private static bool TryGet(string cue, out AudioClip clip)
    {
        clip = null;
        if (instance == null || string.IsNullOrEmpty(cue) || !instance.cues.TryGetValue(cue, out clip))
            return false;
        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        return true;
    }

    public static void Play(string cue, float volume = 1f)
    {
        if (!TryGet(cue, out AudioClip clip)) return;
        // The named exports for these UI cues contain unrelated, multi-second
        // audio. Keep the original cue name at the call site, but use the actual
        // short click asset until the corresponding WAVs are re-exported.
        bool brokenMenuCue = cue.Equals("select", StringComparison.OrdinalIgnoreCase) && clip.length > 1f ||
            cue.Equals("bigDeSelect", StringComparison.OrdinalIgnoreCase) && clip.length > 3f ||
            cue.Equals("cancel", StringComparison.OrdinalIgnoreCase) && clip.length > 3f;
        if (brokenMenuCue && TryGet("smallSelect", out AudioClip click))
        {
            if (instance.lastMenuClickFrame == Time.frameCount) return;
            instance.lastMenuClickFrame = Time.frameCount;
            clip = click;
        }
        instance.effects.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private AudioLowPassFilter musicMuffle;

    private void SetMusicMuffled(bool muffled)
    {
        if (music == null) return;
        if (musicMuffle == null)
        {
            if (!muffled) return;
            musicMuffle = music.gameObject.AddComponent<AudioLowPassFilter>();
            musicMuffle.cutoffFrequency = 900f;
        }
        musicMuffle.enabled = muffled;
    }

    public static void PlayMusic(string cue)
    {
        if (instance == null) return;
        // Only the indoor rain loop is muffled; any other track plays at full frequency.
        if (cue != "rain") instance.SetMusicMuffled(false);
        if (instance.currentMusic == cue) return;
        instance.currentMusic = cue;
        instance.music.Stop();
        instance.music.clip = TryGet(cue, out AudioClip clip) ? clip : null;
        instance.music.loop = cue == "MainTheme" && clip != null && clip.length >= 10f ||
            cue == "rain" ||
            cue != null && cue.EndsWith("_ambient", StringComparison.OrdinalIgnoreCase);
        if (clip != null) instance.music.Play();
    }

    public static void PlayAmbience(string cue)
    {
        if (instance == null || instance.currentAmbience == cue) return;
        instance.currentAmbience = cue;
        instance.ambience.Stop();
        instance.ambience.clip = TryGet(cue, out AudioClip clip) ? clip : null;
        if (clip != null) instance.ambience.Play();
    }

    public static void RefreshLocationMusic()
    {
        if (instance == null) return;
        GameLocation location = LocationManager.Instance?.CurrentLocation;
        string name = location?.LocationName ?? "Farm";
        bool locationChanged = !string.Equals(instance.lastLocationName, name, StringComparison.Ordinal);
        instance.lastLocationName = name;
        PlayAmbience(null);
        instance.SetMusicMuffled(false);
        if (int.TryParse(name, out int mineLevel) && mineLevel > 0 ||
            name.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (mineLevel == 120) { PlayMusic(null); return; }
            string area = mineLevel >= 80 && mineLevel < 120 ? "Lava" :
                mineLevel >= 40 && mineLevel < 80 ? "Frost" : "Upper";
            string ambient = area + "_Ambient";
            if (locationChanged && mineLevel > 2 && mineLevel % 5 != 0 &&
                UnityEngine.Random.value < 0.5f)
                PlayMusic(area == "Upper" ? "EarthMine" : area + "Mine");
            else if (locationChanged || !instance.music.isPlaying || instance.currentMusic == null ||
                instance.currentMusic.EndsWith("_Ambient", StringComparison.OrdinalIgnoreCase))
                PlayMusic(ambient);
            return;
        }
        if (name is "AnimalShop" or "ScienceHouse")
        {
            PlayMusic("marnieShop");
            return;
        }
        if (name == "Saloon") { PlayMusic("Saloon1"); return; }
        if (name == "CommunityCenter") { PlayMusic("communityCenter"); return; }
        if (name == "Hospital") { PlayMusic("Hospital_Ambient"); return; }
        if (name == "Beach-Jellies") { PlayMusic("moonlightJellies"); return; }
        if (name is "Beach-NightMarket" or "Submarine" or "MermaidHouse")
        {
            PlayMusic("night_market");
            return;
        }
        if (location != null && !location.IsOutdoors)
        {
            // On rainy days the rain loop keeps playing indoors, muffled through the walls.
            if (FarmDayClock.Instance?.IsRainingToday == true)
            {
                PlayMusic("rain");
                instance.SetMusicMuffled(true);
            }
            else PlayMusic(null);
            return;
        }
        var clock = FarmDayClock.Instance;
        string season = clock?.CurrentSeason ?? "spring";
        int time = clock?.TimeOfDay ?? 600;
        bool raining = clock?.IsRainingOutdoors(location) == true;
        int day = clock?.Day ?? 1;
        if (raining) { PlayMusic("rain"); return; }

        // The morning song plays once per day and finishes naturally; ambient music follows.
        if (time < 1200 && instance.morningSongDay != day)
        {
            instance.morningSongDay = day;
            PlayMusic(season + (clock?.SongIndex ?? 3));
            return;
        }
        string morningCue = season + (clock?.SongIndex ?? 3);
        if (instance.music.isPlaying && instance.currentMusic == morningCue) return;
        if (name == "Town" && (season is "spring" or "summer") && time < 1800)
        {
            PlayMusic("springtown");
            return;
        }
        if (time >= 1900)
            PlayMusic(season == "winter" ? null : "spring_night_ambient");
        else
            PlayMusic(season + "_day_ambient");
    }
}
