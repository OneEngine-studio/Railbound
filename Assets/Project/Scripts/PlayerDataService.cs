using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Nakama;
using Railbound.UI;
using UnityEngine;

/// <summary>
/// Keeps the player's lobby data in Nakama. After sign-in it reads the player's documents (collection "player_data", plus the existing
/// "player_profiles" and "player_progress"), applies the newer copy of each, and creates whatever is missing from what this device has,
/// so a new guest gets a complete record at once. After that, changes are pushed a moment after the last edit (and when the game pauses
/// or quits).
///
///   profile    (player_profiles/profile)   name and skill level           - PlayerProfileService
///   progress   (player_progress/progress)  level, XP, coins, match stats    - PlayerProgression
///   loadout    (player_data/loadout)       chosen operator, looks, mode, team size
///   settings   (player_data/settings)      graphics, audio, controls, key bindings, language
///   challenges (player_data/challenges)    daily, weekly and season progress - ChallengeService
///   history    (player_data/history)       the last 20 matches
///
/// Last write wins per document, by its "updatedAt" time. The client writes these documents, so a modified client can change them: the
/// authoritative version moves to Nakama server code with the match result reporting.
/// </summary>
public sealed class PlayerDataService : MonoBehaviour
{
    public const string Collection = "player_data";
    public enum Doc { Loadout, Settings, Challenges, History }

    public static PlayerDataService Instance { get; private set; }
    public static event Action Loaded;
    public bool IsLoaded { get; private set; }

    private const float PushDelay = 3f;
    private readonly HashSet<Doc> dirty = new HashSet<Doc>();
    private float lastChange;
    private bool loading, pushing, applying;
    private string loadedFor;

    // ---------------- documents ----------------
    [Serializable] private sealed class LoadoutDoc { public int v = 1; public long updatedAt; public int op; public int[] looks; public string mode; public int team; }
    [Serializable] private sealed class PrefEntry { public string k; public string t; public string s; }   // t: i (int), f (float), s (string)
    [Serializable] private sealed class SettingsDoc { public int v = 1; public long updatedAt; public List<PrefEntry> prefs = new List<PrefEntry>(); }
    [Serializable] private sealed class HistoryEntry { public long at; public string mode; public int place, total, kills, sec, xp, coins; public bool won; }
    [Serializable] private sealed class HistoryDoc { public int v = 1; public long updatedAt; public List<HistoryEntry> items = new List<HistoryEntry>(); }

    private HistoryDoc history = new HistoryDoc();
    private int lastOperator;

    private static string Key(Doc d) { return d.ToString().ToLowerInvariant(); }
    private static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
    private static long LocalAt(Doc d) { long.TryParse(PlayerPrefs.GetString("oe.at." + Key(d), "0"), out var t); return t; }
    private static void SetLocalAt(Doc d, long t) { PlayerPrefs.SetString("oe.at." + Key(d), t.ToString(CultureInfo.InvariantCulture)); }

    // ---------------- start ----------------
    public static void EnsureStarted()
    {
        if (Instance != null) return;
        var go = new GameObject("[Player Data]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PlayerDataService>();
    }

    private void Update()
    {
        var auth = AuthenticationService.Instance;
        bool signedIn = auth != null && auth.IsAuthenticated;
        if (!signedIn) { if (IsLoaded) { IsLoaded = false; loadedFor = null; } return; }

        if (!loading && (!IsLoaded || loadedFor != auth.CurrentUserId)) { _ = LoadAllAsync(); return; }
        if (IsLoaded && dirty.Count > 0 && !pushing && Time.unscaledTime - lastChange > PushDelay) _ = PushAsync();
    }

    private void OnApplicationPause(bool paused) { if (paused && IsLoaded && dirty.Count > 0 && !pushing) _ = PushAsync(); }
    private void OnApplicationQuit() { if (IsLoaded && dirty.Count > 0 && !pushing) _ = PushAsync(); }

    public void MarkDirty(Doc d)
    {
        if (applying) return;   // a change that came from the cloud is not pushed back
        dirty.Add(d);
        lastChange = Time.unscaledTime;
    }

    // ---------------- loading ----------------
    private async Task LoadAllAsync()
    {
        loading = true;
        var auth = AuthenticationService.Instance;
        string user = auth.CurrentUserId;
        try
        {
            // profile: create it from the session's name when there is none yet (a guest has none)
            try
            {
                var profile = await PlayerProfileService.LoadAsync();
                if (profile == null)
                {
                    string name = auth.Session != null ? auth.Session.Username : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (name.Length > 16) name = name.Substring(0, 16);
                        if (name.Length < 3) name = name.PadRight(3, '_');
                        await PlayerProfileService.SaveAsync(name, PlayerExperienceLevel.Inexpert);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[PlayerData] Profile: " + ex.Message); }

            await PlayerProgression.LoadAsync();   // reads, merges and creates the progress record

            var ids = new List<IApiReadStorageObjectId>();
            foreach (Doc d in Enum.GetValues(typeof(Doc))) ids.Add(new StorageObjectId { Collection = Collection, Key = Key(d), UserId = user });
            var result = await auth.Client.ReadStorageObjectsAsync(auth.Session, ids.ToArray());
            var remote = new Dictionary<string, string>();
            foreach (var o in result.Objects) remote[o.Key] = o.Value;

            applying = true;
            try
            {
                LoadLoadout(remote);
                LoadSettings(remote);
                LoadChallenges(remote);
                LoadHistory(remote);
            }
            finally { applying = false; }

            IsLoaded = true;
            loadedFor = user;
            Debug.Log("[PlayerData] Loaded for " + user + " (" + remote.Count + " documents in the cloud, " + dirty.Count + " to create)");
            if (Loaded != null) Loaded();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PlayerData] Load failed: " + ex.Message);
            await Task.Delay(5000);   // try again later
        }
        finally { loading = false; }
    }

    private void LoadLoadout(Dictionary<string, string> remote)
    {
        LoadoutDoc doc = null;
        if (remote.TryGetValue(Key(Doc.Loadout), out var json)) { try { doc = JsonUtility.FromJson<LoadoutDoc>(json); } catch (Exception) { } }
        if (doc != null && doc.updatedAt > LocalAt(Doc.Loadout))
        {
            lastOperator = Mathf.Max(0, doc.op);
            LobbyOperatorLooks.Import(doc.looks);
            var mode = GameModeCatalog.Find(doc.mode);
            if (mode != null) GameModeCatalog.TrySelect(mode, (TeamSize)Mathf.Clamp(doc.team, 1, 4));
            SetLocalAt(Doc.Loadout, doc.updatedAt);
            ApplyLoadoutToLobby();
        }
        else if (doc == null) { dirty.Add(Doc.Loadout); lastChange = -PushDelay; }   // not in the cloud yet: create it now
    }

    private void LoadSettings(Dictionary<string, string> remote)
    {
        SettingsDoc doc = null;
        if (remote.TryGetValue(Key(Doc.Settings), out var json)) { try { doc = JsonUtility.FromJson<SettingsDoc>(json); } catch (Exception) { } }
        if (doc != null && doc.updatedAt > LocalAt(Doc.Settings))
        {
            foreach (var e in doc.prefs)
            {
                if (string.IsNullOrEmpty(e.k)) continue;
                if (e.t == "i" && int.TryParse(e.s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) PlayerPrefs.SetInt(e.k, i);
                else if (e.t == "f" && float.TryParse(e.s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) PlayerPrefs.SetFloat(e.k, f);
                else if (e.t == "s") PlayerPrefs.SetString(e.k, e.s ?? "");
            }
            PlayerPrefs.Save();
            SetLocalAt(Doc.Settings, doc.updatedAt);
            foreach (var gm in Resources.FindObjectsOfTypeAll<Michsky.UI.Reach.GraphicsManager>())
                if (gm != null && gm.gameObject.scene.IsValid()) gm.LoadAndApplyAllSettings();
        }
        else if (doc == null) { dirty.Add(Doc.Settings); lastChange = -PushDelay; }
    }

    private void LoadChallenges(Dictionary<string, string> remote)
    {
        if (remote.TryGetValue(Key(Doc.Challenges), out var json)) ChallengeService.ApplyRemote(json);
        else { dirty.Add(Doc.Challenges); lastChange = -PushDelay; }
    }

    private void LoadHistory(Dictionary<string, string> remote)
    {
        if (remote.TryGetValue(Key(Doc.History), out var json))
        {
            try { var doc = JsonUtility.FromJson<HistoryDoc>(json); if (doc != null && doc.items != null) history = doc; } catch (Exception) { }
        }
        else { dirty.Add(Doc.History); lastChange = -PushDelay; }
    }

    // ---------------- the lobby <-> loadout ----------------
    /// <summary>Puts the saved operator on stage and refreshes the look (the lobby calls this once it is ready).</summary>
    public void ApplyLoadoutToLobby()
    {
        var preview = LobbyCharacterPreviewManager.Instance;
        if (preview == null) return;
        bool was = applying; applying = true;
        try
        {
            if (lastOperator != preview.CurrentOperatorIndex && lastOperator < preview.TotalOperators) preview.SelectOperator(lastOperator);
            else preview.RefreshLook();
        }
        finally { applying = was; }
    }

    /// <summary>Starts listening to the lobby's choices (operator, looks, mode). Call from the lobby; undo with <see cref="DetachLobby"/>.</summary>
    public void AttachLobby()
    {
        DetachLobby();
        var preview = LobbyCharacterPreviewManager.Instance;
        if (preview != null) preview.OnOperatorChanged += OnOperatorChanged;
        LobbyOperatorLooks.Changed += OnLoadoutChanged;
        GameModeCatalog.Changed += OnLoadoutChanged;
        if (IsLoaded) ApplyLoadoutToLobby();
    }

    public void DetachLobby()
    {
        var preview = LobbyCharacterPreviewManager.Instance;
        if (preview != null) preview.OnOperatorChanged -= OnOperatorChanged;
        LobbyOperatorLooks.Changed -= OnLoadoutChanged;
        GameModeCatalog.Changed -= OnLoadoutChanged;
    }

    private void OnOperatorChanged(OperatorDefinition op)
    {
        var preview = LobbyCharacterPreviewManager.Instance;
        if (preview != null) lastOperator = preview.CurrentOperatorIndex;
        OnLoadoutChanged();
    }

    private void OnLoadoutChanged() { MarkDirty(Doc.Loadout); }

    /// <summary>The settings screen closed (or something else changed a setting): push the settings document.</summary>
    public void SettingsChanged() { MarkDirty(Doc.Settings); }

    // ---------------- history ----------------
    public void RecordMatch(MatchSummary m, int xp, int coins)
    {
        history.items.Add(new HistoryEntry { at = Now(), mode = m.mode, place = m.won ? 1 : m.placement, total = m.totalPlayers, kills = m.kills, sec = Mathf.RoundToInt(m.survivedSeconds), xp = xp, coins = coins, won = m.won });
        while (history.items.Count > 20) history.items.RemoveAt(0);
        MarkDirty(Doc.History);
    }

    // ---------------- pushing ----------------
    private async Task PushAsync()
    {
        var auth = AuthenticationService.Instance;
        if (auth == null || !auth.IsAuthenticated) return;
        pushing = true;
        var sending = dirty.ToList();
        try
        {
            var writes = new List<IApiWriteStorageObject>();
            long now = Now();
            foreach (var d in sending)
            {
                string value = null;
                switch (d)
                {
                    case Doc.Loadout: value = JsonUtility.ToJson(CaptureLoadout(now)); break;
                    case Doc.Settings: value = JsonUtility.ToJson(CaptureSettings(now)); break;
                    case Doc.Challenges: value = ChallengeService.ToJson(); break;
                    case Doc.History: history.updatedAt = now; value = JsonUtility.ToJson(history); break;
                }
                if (value == null) continue;
                writes.Add(new WriteStorageObject { Collection = Collection, Key = Key(d), Value = value, PermissionRead = 1, PermissionWrite = 1 });
            }
            if (writes.Count > 0) await auth.Client.WriteStorageObjectsAsync(auth.Session, writes.ToArray());
            foreach (var d in sending) { dirty.Remove(d); if (d == Doc.Loadout || d == Doc.Settings) SetLocalAt(d, now); }
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PlayerData] Push failed (will retry): " + ex.Message);
            lastChange = Time.unscaledTime;
        }
        finally { pushing = false; }
    }

    private LoadoutDoc CaptureLoadout(long now)
    {
        var preview = LobbyCharacterPreviewManager.Instance;
        if (preview != null) lastOperator = preview.CurrentOperatorIndex;
        var mode = GameModeCatalog.Selected;
        return new LoadoutDoc { updatedAt = now, op = lastOperator, looks = LobbyOperatorLooks.Export(), mode = mode.Id, team = (int)GameModeCatalog.SelectedTeamSize };
    }

    // settings keys: the ones the game writes itself, plus every key a Reach control of the settings menu saves under
    private static readonly string[] GraphicsFloats = { "Graphics_ExposureComp", "Graphics_Brightness", "Graphics_FSRSharpness", "Graphics_FilmGrain", "Graphics_FOV", "Graphics_VRAMTarget" };
    private static readonly string[] GraphicsInts =
    {
        "Graphics_VSync", "Graphics_FrameRate", "Graphics_WindowMode", "Graphics_TextureQuality", "Graphics_AnisotropicFiltering", "Graphics_ExposureMode",
        "Graphics_ExposureAdapt", "Graphics_DLSS", "Graphics_DLSSQuality", "Graphics_VolumetricFog", "Graphics_Fog", "Graphics_AntiAliasing",
        "Graphics_RenderScaleIndex", "Graphics_FSR", "Graphics_MeshQuality", "Graphics_SSAO", "Graphics_ShadowQuality", "Graphics_ShadowDistanceIndex",
        "Graphics_MotionBlur", "Graphics_Bloom", "Graphics_DepthOfField", "Graphics_VRAMTargetIndex", "Graphics_TextureStreaming", "Graphics_AsyncUpload",
    };

    private SettingsDoc CaptureSettings(long now)
    {
        var doc = new SettingsDoc { updatedAt = now };
        foreach (var k in GraphicsFloats) if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "f", s = PlayerPrefs.GetFloat(k).ToString("R", CultureInfo.InvariantCulture) });
        foreach (var k in GraphicsInts) if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "i", s = PlayerPrefs.GetInt(k).ToString(CultureInfo.InvariantCulture) });
        foreach (var k in new[] { "Player_Heal_Key", "Player_Sonar_Key", JUTPS.JUInputSystem.JUInputManager.BINDING_OVERRIDES_KEY, Michsky.UI.Reach.UIManager.localizationSaveKey })
            if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "s", s = PlayerPrefs.GetString(k) });

        var menu = LobbyReachSettings.Find();
        if (menu != null)
        {
            foreach (var c in menu.GetComponentsInChildren<Michsky.UI.Reach.SwitchManager>(true)) { string k = "Switch_" + c.saveKey; if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "s", s = PlayerPrefs.GetString(k) }); }
            foreach (var c in menu.GetComponentsInChildren<Michsky.UI.Reach.SliderManager>(true)) { string k = "Slider_" + c.saveKey; if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "f", s = PlayerPrefs.GetFloat(k).ToString("R", CultureInfo.InvariantCulture) }); }
            foreach (var c in menu.GetComponentsInChildren<Michsky.UI.Reach.HorizontalSelector>(true)) { string k = "HorizontalSelector_" + c.saveKey; if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "i", s = PlayerPrefs.GetInt(k).ToString(CultureInfo.InvariantCulture) }); }
            foreach (var c in menu.GetComponentsInChildren<Michsky.UI.Reach.Dropdown>(true)) { string k = "Dropdown_" + c.saveKey; if (PlayerPrefs.HasKey(k)) doc.prefs.Add(new PrefEntry { k = k, t = "i", s = PlayerPrefs.GetInt(k).ToString(CultureInfo.InvariantCulture) }); }
        }
        // no duplicates (several controls can share a key)
        doc.prefs = doc.prefs.GroupBy(p => p.k).Select(g => g.First()).ToList();
        return doc;
    }
}
