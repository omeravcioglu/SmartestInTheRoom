using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using Smartest.UI;
using Object = UnityEngine.Object;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Smartest.EditorTools
{
    /// <summary>
    /// Tools > Smartest > Build Scenes.
    ///
    ///  - folder layout, rounded 9-slice sprite, GameConfig asset
    ///  - 25 RoundDefinition assets (Data/Rounds) + RoundLibrary (Data) from RoundCatalog
    ///  - Prefabs: Resources/Bootstrap (GameBootstrap + NetworkManager + UnityTransport + NetSession + VoiceLines),
    ///             Prefabs/PlayerData (NetworkObject + PlayerData), Prefabs/PlayerRow, Prefabs/RevealRow
    ///  - Menu.unity: menu / join / lobby, fully wired
    ///  - Game.unity: GameState (in-scene NetworkObject), scoreboard, round / reveal / winner panels, fully wired
    /// Re-running is safe: assets are updated in place (GUIDs stay stable), scenes are rebuilt.
    /// </summary>
    public static class SceneBuilder
    {
        private const string Root = "Assets/_Smartest";
        private const string ScenesDir = Root + "/Scenes";
        private const string MenuScenePath = ScenesDir + "/Menu.unity";
        private const string GameScenePath = ScenesDir + "/Game.unity";
        private const string SpritePath = Root + "/Sprites/RoundedPanel.png";
        private const string ConfigPath = Root + "/Data/GameConfig.asset";
        private const string LibraryPath = Root + "/Resources/RoundLibrary.asset"; // loadable by name at runtime
        private const string RoundsDir = Root + "/Data/Rounds";
        private const string VoiceDir = Root + "/Audio/Voice";
        private const string BootstrapPrefabPath = Root + "/Resources/Bootstrap.prefab";
        private const string GameStatePrefabPath = Root + "/Resources/GameState.prefab";
        private const string PlayerDataPrefabPath = Root + "/Prefabs/PlayerData.prefab";
        private const string PlayerRowPrefabPath = Root + "/Prefabs/PlayerRow.prefab";
        private const string RevealRowPrefabPath = Root + "/Prefabs/RevealRow.prefab";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        private const float RefW = 1920f;
        private const float RefH = 1080f;

        [MenuItem("Tools/Smartest/Build Scenes")]
        public static void BuildScenes()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog(
                    "Import TMP Essentials first",
                    "TextMeshPro isn't set up in this project yet, so text would render blank.\n\n" +
                    "Do this once: Window > TextMeshPro > Import TMP Essential Resources,\n" +
                    "then run Tools > Smartest > Build Scenes again.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(MenuScenePath) || File.Exists(GameScenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Rebuild scenes?",
                    "Menu.unity / Game.unity already exist under _Smartest/Scenes.\n\nRebuild them (prefabs and round assets are updated in place)?",
                    "Rebuild", "Cancel");
                if (!ok) return;
            }

            EnsureFolders();
            Sprite rounded = GenerateRoundedSprite(SpritePath, 64, 16f);
            GameConfig config = EnsureGameConfig();
            RoundLibrary library = BuildRoundAssets();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject playerDataPrefab = BuildPlayerDataPrefab();
            GameObject gameStatePrefab = BuildGameStatePrefab();
            GameObject playerRowPrefab = BuildPlayerRowPrefab(rounded);
            GameObject revealRowPrefab = BuildRevealRowPrefab(rounded);
            BuildBootstrapPrefab(config, playerDataPrefab, gameStatePrefab);
            AssetDatabase.SaveAssets();

            BuildMenuScene(rounded, config, playerRowPrefab);
            BuildGameScene(rounded, config, playerRowPrefab, revealRowPrefab);

            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(MenuScenePath);

            Debug.Log($"[Smartest] Build complete: {library.rounds.Count} rounds, prefabs, Menu (0) + Game (1). Press Play in Menu.");
        }

        // ------------------------------------------------------------------
        // Folders & simple assets
        // ------------------------------------------------------------------

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root, ScenesDir,
                Root + "/Scripts", Root + "/Scripts/Core", Root + "/Scripts/Net",
                Root + "/Scripts/Rounds", Root + "/Scripts/UI",
                Root + "/Scripts/Minigames", Root + "/Scripts/Minigames/Core", Root + "/Scripts/Minigames/Games",
                Root + "/Editor", Root + "/Prefabs",
                Root + "/Data", RoundsDir,
                Root + "/Audio", VoiceDir, Root + "/Audio/Music",
                Root + "/Resources", Root + "/Sprites",
                Root + "/Tests", Root + "/Tests/EditMode"
            };
            foreach (var f in folders) EnsureFolder(f);
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static GameConfig EnsureGameConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return cfg;
        }

        /// <summary>
        /// Creates/updates one asset per challenge — social rounds from RoundCatalog and one
        /// per registered minigame — then lists them all in the library. Assets for rounds or
        /// minigames that no longer exist are deleted, so removing a challenge is just a
        /// matter of removing it from the catalog or the registry and rebuilding.
        /// </summary>
        private static RoundLibrary BuildRoundAssets()
        {
            var library = AssetDatabase.LoadAssetAtPath<RoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<RoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.rounds.Clear();

            var wanted = new HashSet<string>();
            foreach (var spec in RoundCatalog.AllChallenges())
            {
                string prefix = spec.Kind == RoundKind.Minigame ? $"M{spec.Id:000}" : $"C{spec.Id:00}";
                string file = $"{RoundsDir}/{prefix}_{Sanitize(spec.Title)}.asset";
                wanted.Add(file);

                var def = AssetDatabase.LoadAssetAtPath<RoundDefinition>(file);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<RoundDefinition>();
                    AssetDatabase.CreateAsset(def, file);
                }
                spec.ApplyTo(def);
                EditorUtility.SetDirty(def);
                library.rounds.Add(def);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:RoundDefinition", new[] { RoundsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (wanted.Contains(path)) continue;
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[Smartest] Removed stale round asset {Path.GetFileName(path)}.");
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static Sprite GenerateRoundedSprite(string assetPath, int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;
            var halfExtents = new Vector2(half, half);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    var d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfExtents - new Vector2(radius, radius));
                    float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
                    float sd = outside + inside - radius;
                    float a = Mathf.Clamp01(0.5f - sd);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            string sysPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(sysPath));
            File.WriteAllBytes(sysPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(radius, radius, radius, radius);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void AddScenesToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == MenuScenePath || s.path == GameScenePath) continue;
                list.Add(s);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------

        private static GameObject BuildPlayerDataPrefab()
        {
            var go = new GameObject("PlayerData");
            var netObj = go.AddComponent<NetworkObject>();
            netObj.DestroyWithScene = false; // survive Menu -> Game (Netcode migrates it to DontDestroyOnLoad)
            go.AddComponent<PlayerData>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerDataPrefabPath);
            Object.DestroyImmediate(go);
            RegenerateNetcodeHash(prefab);
            return prefab;
        }

        private static GameObject BuildGameStatePrefab()
        {
            // Reload by path so we never hold a stale reference across asset imports.
            var library = AssetDatabase.LoadAssetAtPath<RoundLibrary>(LibraryPath);
            var go = new GameObject("GameState");
            go.AddComponent<NetworkObject>();
            var gs = go.AddComponent<GameState>();
            gs.EditorSetLibrary(library);
            SetPrivate(gs, "library", library);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, GameStatePrefabPath);
            Object.DestroyImmediate(go);
            RegenerateNetcodeHash(prefab);

            var saved = prefab.GetComponent<GameState>();
            var so = new SerializedObject(saved);
            var libProp = so.FindProperty("library");
            if (libProp == null || libProp.objectReferenceValue == null)
                Debug.LogWarning("[Smartest] GameState prefab has no RoundLibrary reference; it will fall back to Resources/RoundLibrary at runtime.");
            return prefab;
        }

        /// <summary>
        /// Netcode assigns each network prefab a GlobalObjectIdHash inside NetworkObject.OnValidate,
        /// which Unity only calls for objects edited in the Inspector — never for prefabs saved from
        /// script. A hash of 0 makes Netcode refuse to spawn the object. So after saving, we invoke
        /// Netcode's own generator on the saved asset (it's internal, hence reflection) and verify.
        /// </summary>
        private static void RegenerateNetcodeHash(GameObject prefabAsset)
        {
            var netObj = prefabAsset != null ? prefabAsset.GetComponent<NetworkObject>() : null;
            if (netObj == null) return;

            var method = typeof(NetworkObject).GetMethod("OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                try { method.Invoke(netObj, null); }
                catch (System.Exception e) { Debug.LogWarning($"[Smartest] NetworkObject.OnValidate threw: {e.Message}"); }
            }

            var so = new SerializedObject(netObj);
            var prop = so.FindProperty("GlobalObjectIdHash");
            uint hash = prop != null ? prop.uintValue : 0u;

            if (hash == 0 && prop != null)
            {
                // Fallback: compute it the same way Netcode does (xxHash32 of the GlobalObjectId string).
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(netObj);
                if (gid.identifierType != 0)
                {
                    hash = XxHash32(Encoding.UTF8.GetBytes(gid.ToString()));
                    prop.uintValue = hash;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(netObj);
            AssetDatabase.SaveAssets();

            if (hash == 0)
                Debug.LogError($"[Smartest] Could not generate a Netcode GlobalObjectIdHash for {prefabAsset.name}. Select the prefab in the Project window once (Netcode fills it in), then re-run Build Scenes.");
            else
                Debug.Log($"[Smartest] {prefabAsset.name}.prefab GlobalObjectIdHash = {hash}");
        }

        private static uint XxHash32(byte[] data, uint seed = 0)
        {
            const uint P1 = 2654435761U, P2 = 2246822519U, P3 = 3266489917U, P4 = 668265263U, P5 = 374761393U;
            int len = data.Length, i = 0;
            uint h;
            if (len >= 16)
            {
                uint v1 = seed + P1 + P2, v2 = seed + P2, v3 = seed, v4 = seed - P1;
                while (i <= len - 16)
                {
                    v1 = Rotl(v1 + ReadU32(data, i) * P2, 13) * P1; i += 4;
                    v2 = Rotl(v2 + ReadU32(data, i) * P2, 13) * P1; i += 4;
                    v3 = Rotl(v3 + ReadU32(data, i) * P2, 13) * P1; i += 4;
                    v4 = Rotl(v4 + ReadU32(data, i) * P2, 13) * P1; i += 4;
                }
                h = Rotl(v1, 1) + Rotl(v2, 7) + Rotl(v3, 12) + Rotl(v4, 18);
            }
            else h = seed + P5;
            h += (uint)len;
            while (i <= len - 4) { h += ReadU32(data, i) * P3; h = Rotl(h, 17) * P4; i += 4; }
            while (i < len) { h += data[i] * P5; h = Rotl(h, 11) * P1; i++; }
            h ^= h >> 15; h *= P2; h ^= h >> 13; h *= P3; h ^= h >> 16;
            return h;
        }

        private static uint Rotl(uint x, int r) => (x << r) | (x >> (32 - r));
        private static uint ReadU32(byte[] b, int i) => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));

        private static void BuildBootstrapPrefab(GameConfig config, GameObject playerDataPrefab, GameObject gameStatePrefab)
        {
            // Reload by path: asset imports in between can leave earlier references stale.
            playerDataPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerDataPrefabPath) ?? playerDataPrefab;
            gameStatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameStatePrefabPath) ?? gameStatePrefab;
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) ?? config;

            var go = new GameObject("Bootstrap");
            var boot = go.AddComponent<GameBootstrap>();
            SetPrivate(boot, "config", config);

            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = playerDataPrefab;
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.ConnectionApproval = false;

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultNetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, DefaultNetworkPrefabsPath);
            }
            foreach (var p in new[] { playerDataPrefab, gameStatePrefab })
            {
                if (p != null && !list.Contains(p))
                {
                    list.Add(new NetworkPrefab { Prefab = p });
                    EditorUtility.SetDirty(list);
                }
            }
            if (nm.NetworkConfig.Prefabs.NetworkPrefabsLists == null)
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList>();
            if (!nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);

            go.AddComponent<NetSession>();

            // Three audio channels the player can set independently.
            var director = go.AddComponent<AudioDirector>();
            var narratorSource = MakeAudioChannel(go.transform, "Narrator");
            var musicSource = MakeAudioChannel(go.transform, "Music");
            var sfxSource = MakeAudioChannel(go.transform, "Sfx");
            musicSource.loop = true;
            director.EditorSetSources(narratorSource, musicSource, sfxSource, VoicePaths.FindMusicLoop());

            // The host script; every clip that has been recorded gets wired to its line.
            var voice = go.AddComponent<VoiceLines>();
            var entries = VoiceLines.EntriesFromScript();
            int recorded = 0, wanted = 0;
            foreach (var e in entries)
            {
                for (int i = 0; i < e.clips.Length; i++)
                {
                    wanted++;
                    e.clips[i] = VoicePaths.Load(e.key, i);
                    if (e.clips[i] != null) recorded++;
                }
            }
            voice.EditorSetLines(entries);
            Debug.Log($"[Smartest] Voice: {recorded}/{wanted} clips wired. " +
                      (recorded < wanted ? "Record the rest with Tools > Smartest > Voice Studio." : "Full script recorded."));

            PrefabUtility.SaveAsPrefabAsset(go, BootstrapPrefabPath);
            Object.DestroyImmediate(go);
        }

        private static AudioSource MakeAudioChannel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(parent, false);
            var src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        private static GameObject BuildPlayerRowPrefab(Sprite rounded)
        {
            var go = new GameObject("PlayerRow", typeof(RectTransform));
            var bg = go.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Palette.PanelRaised;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 52;
            le.minHeight = 52;
            le.flexibleWidth = 1;

            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(18, 18, 6, 6);
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var name = MakeText("Name", go.transform, "Player", 22, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var nameLe = name.gameObject.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;
            nameLe.minWidth = 40;

            var tag = MakeText("Tag", go.transform, "HOST", 14, Palette.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            var state = MakeText("State", go.transform, "thinking", 15, Palette.TextDim, TextAlignmentOptions.Right, FontStyles.Italic);
            var delta = MakeText("Delta", go.transform, "+15", 20, Palette.Positive, TextAlignmentOptions.Right, FontStyles.Bold);
            delta.gameObject.SetActive(false);
            var score = MakeText("Score", go.transform, "0", 24, Palette.Text, TextAlignmentOptions.Right, FontStyles.Bold);

            var view = go.AddComponent<PlayerRowView>();
            SetPrivate(view, "background", bg);
            SetPrivate(view, "nameText", name);
            SetPrivate(view, "tagText", tag);
            SetPrivate(view, "stateText", state);
            SetPrivate(view, "deltaText", delta);
            SetPrivate(view, "scoreText", score);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerRowPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildRevealRowPrefab(Sprite rounded)
        {
            var go = new GameObject("RevealRow", typeof(RectTransform));
            var group = go.AddComponent<CanvasGroup>();
            var bg = go.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Palette.PanelRaised;

            var name = MakeText("Name", go.transform, "Player", 24, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            name.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(name.rectTransform, 24, 4, 380, 4);

            var chip = new GameObject("Chip", typeof(RectTransform));
            chip.transform.SetParent(go.transform, false);
            var chipImg = chip.AddComponent<Image>();
            chipImg.sprite = rounded;
            chipImg.type = Image.Type.Sliced;
            chipImg.color = Palette.Neutral;
            var chipRt = chip.GetComponent<RectTransform>();
            chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0.5f, 0.5f);
            chipRt.sizeDelta = new Vector2(150, 42);
            chipRt.anchoredPosition = new Vector2(60f, 0f);
            var chipText = MakeText("ChipText", chip.transform, "GREEN", 22, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(chipText.rectTransform);

            var note = MakeText("Note", go.transform, "WINNER", 13, Palette.Accent, TextAlignmentOptions.Right, FontStyles.Bold);
            var noteRt = note.rectTransform;
            noteRt.anchorMin = noteRt.anchorMax = new Vector2(1f, 0.5f);
            noteRt.pivot = new Vector2(1f, 0.5f);
            noteRt.sizeDelta = new Vector2(110, 40);
            noteRt.anchoredPosition = new Vector2(-120f, 0f);
            note.gameObject.SetActive(false);

            var delta = MakeText("Delta", go.transform, "+10", 28, Palette.Positive, TextAlignmentOptions.Right, FontStyles.Bold);
            var dRt = delta.rectTransform;
            dRt.anchorMin = dRt.anchorMax = new Vector2(1f, 0.5f);
            dRt.pivot = new Vector2(1f, 0.5f);
            dRt.sizeDelta = new Vector2(100, 44);
            dRt.anchoredPosition = new Vector2(-20f, 0f);

            var view = go.AddComponent<RevealRowView>();
            SetPrivate(view, "group", group);
            SetPrivate(view, "nameText", name);
            SetPrivate(view, "chipBackground", chipImg);
            SetPrivate(view, "chipText", chipText);
            SetPrivate(view, "deltaText", delta);
            SetPrivate(view, "noteText", note);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, RevealRowPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------
        // Menu scene
        // ------------------------------------------------------------------

        private static void BuildMenuScene(Sprite rounded, GameConfig config, GameObject playerRowPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);

            var main = MakePanel<Panel>("MainMenuPanel", canvas.transform, new Vector2(760, 820), Vector2.zero, rounded, config);
            PlaceTop(MakeText("Title", main.transform, "SMARTEST\nIN THE ROOM", 58, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold).rectTransform, 44, new Vector2(700, 160));
            PlaceTop(MakeText("Subtitle", main.transform, "First to 100 points is the smartest in the group.", 22, Palette.TextDim, TextAlignmentOptions.Top).rectTransform, 210, new Vector2(680, 40));
            var nameField = MakeInputField("NameField", main.transform, "Your name", 12, TMP_InputField.ContentType.Standard, rounded);
            PlaceTop(nameField.GetComponent<RectTransform>(), 290, new Vector2(560, 72));
            var hostBtn = MakeButton("HostButton", main.transform, "HOST", new Vector2(560, 84), Palette.Accent, Palette.TextOnAccent, rounded);
            PlaceTop(hostBtn.GetComponent<RectTransform>(), 400, new Vector2(560, 84));
            var joinBtn = MakeButton("JoinButton", main.transform, "JOIN", new Vector2(560, 84), Palette.PanelRaised, Palette.Text, rounded);
            PlaceTop(joinBtn.GetComponent<RectTransform>(), 504, new Vector2(560, 84));
            var quitBtn = MakeButton("QuitButton", main.transform, "QUIT", new Vector2(560, 68), Palette.PanelRaised, Palette.TextDim, rounded);
            PlaceTop(quitBtn.GetComponent<RectTransform>(), 608, new Vector2(560, 68));
            var mainStatus = MakeText("Status", main.transform, "", 20, Palette.Accent, TextAlignmentOptions.Top);
            PlaceTop(mainStatus.rectTransform, 692, new Vector2(680, 44));
            PlaceTop(MakeText("Footer", main.transform, "No in-game chat. Talk on Discord.", 18, Palette.TextDim, TextAlignmentOptions.Top).rectTransform, 748, new Vector2(680, 30));

            var join = MakePanel<Panel>("JoinPanel", canvas.transform, new Vector2(600, 540), Vector2.zero, rounded, config);
            PlaceTop(MakeText("JoinTitle", join.transform, "JOIN A GAME", 40, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold).rectTransform, 44, new Vector2(540, 60));
            // Accepts a Relay code, a host's LAN IP, or LOCAL — so it must allow dots and be long.
            var codeField = MakeInputField("CodeField", join.transform, "CODE OR IP", 24, TMP_InputField.ContentType.Standard, rounded, 30, true);
            PlaceTop(codeField.GetComponent<RectTransform>(), 140, new Vector2(460, 88));
            var goBtn = MakeButton("JoinGoButton", join.transform, "GO", new Vector2(460, 76), Palette.Accent, Palette.TextOnAccent, rounded);
            PlaceTop(goBtn.GetComponent<RectTransform>(), 252, new Vector2(460, 76));
            var joinBackBtn = MakeButton("JoinBackButton", join.transform, "BACK", new Vector2(460, 60), Palette.PanelRaised, Palette.TextDim, rounded);
            PlaceTop(joinBackBtn.GetComponent<RectTransform>(), 340, new Vector2(460, 60));
            PlaceTop(MakeText("JoinHint", join.transform,
                "The host's code, or their IP on the same Wi-Fi.\nType LOCAL for a second instance on this PC.",
                17, Palette.TextDim, TextAlignmentOptions.Top).rectTransform, 414, new Vector2(540, 56));
            var joinStatus = MakeText("Status", join.transform, "", 18, Palette.Accent, TextAlignmentOptions.Top);
            PlaceTop(joinStatus.rectTransform, 476, new Vector2(540, 44));

            var lobby = MakePanel<LobbyUI>("LobbyPanel", canvas.transform, new Vector2(760, 900), Vector2.zero, rounded, config);
            PlaceTop(MakeText("LobbyTitle", lobby.transform, "LOBBY", 30, Palette.TextDim, TextAlignmentOptions.Top, FontStyles.Bold).rectTransform, 34, new Vector2(680, 40));
            var codeText = MakeText("SessionCode", lobby.transform, "----", 72, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            codeText.characterSpacing = 18;
            PlaceTop(codeText.rectTransform, 82, new Vector2(560, 96));
            var copyBtn = MakeButton("CopyButton", lobby.transform, "COPY", new Vector2(120, 52), Palette.PanelRaised, Palette.Text, rounded);
            PlaceTop(copyBtn.GetComponent<RectTransform>(), 104, new Vector2(120, 52)).anchoredPosition = new Vector2(300, -104);
            var modeText = MakeText("Mode", lobby.transform, "Share this code on Discord", 18, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(modeText.rectTransform, 184, new Vector2(680, 30));
            var rows = MakeRowsContainer("Rows", lobby.transform, 6f, true);
            PlaceTop(rows, 232, new Vector2(620, 458));
            var hint = MakeText("Hint", lobby.transform, "Waiting for host…", 20, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(hint.rectTransform, 700, new Vector2(680, 30));
            var startBtn = MakeButton("StartButton", lobby.transform, "START", new Vector2(560, 84), Palette.Accent, Palette.TextOnAccent, rounded);
            PlaceTop(startBtn.GetComponent<RectTransform>(), 742, new Vector2(560, 84));
            var leaveBtn = MakeButton("LeaveButton", lobby.transform, "LEAVE", new Vector2(560, 60), Palette.PanelRaised, Palette.TextDim, rounded);
            PlaceTop(leaveBtn.GetComponent<RectTransform>(), 834, new Vector2(560, 60));

            var rowView = playerRowPrefab.GetComponent<PlayerRowView>();
            SetPrivate(lobby, "codeText", codeText);
            SetPrivate(lobby, "modeText", modeText);
            SetPrivate(lobby, "copyButton", copyBtn);
            SetPrivate(lobby, "rowsContainer", rows);
            SetPrivate(lobby, "rowPrefab", rowView);
            SetPrivate(lobby, "hintText", hint);
            SetPrivate(lobby, "startButton", startBtn);
            SetPrivate(lobby, "leaveButton", leaveBtn);

            // Sound settings, built last so it sits above the menu and the lobby.
            BuildSettingsPanel(canvas.transform, rounded, config);

            var menu = new GameObject("MenuController").AddComponent<MenuUI>();
            SetPrivate(menu, "mainPanel", main);
            SetPrivate(menu, "joinPanel", join);
            SetPrivate(menu, "lobby", lobby);
            SetPrivate(menu, "nameField", nameField);
            SetPrivate(menu, "hostButton", hostBtn);
            SetPrivate(menu, "joinButton", joinBtn);
            SetPrivate(menu, "quitButton", quitBtn);
            SetPrivate(menu, "mainStatus", mainStatus);
            SetPrivate(menu, "codeField", codeField);
            SetPrivate(menu, "joinGoButton", goBtn);
            SetPrivate(menu, "joinBackButton", joinBackBtn);
            SetPrivate(menu, "joinStatus", joinStatus);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        // ------------------------------------------------------------------
        // Game scene
        // ------------------------------------------------------------------

        private static void BuildGameScene(Sprite rounded, GameConfig config,
            GameObject playerRowPrefab, GameObject revealRowPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);

            // (GameState is a network prefab spawned by the host after everyone loads this scene — see NetSession.)

            // --- Round counter (top-left, above the scoreboard) ---
            var counter = MakeText("RoundCounter", canvas.transform, "ROUND 1", 22, Palette.TextDim, TextAlignmentOptions.Left, FontStyles.Bold);
            var cRt = counter.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 1f);
            cRt.pivot = new Vector2(0f, 1f);
            cRt.sizeDelta = new Vector2(380, 36);
            cRt.anchoredPosition = new Vector2(48f, -14f);

            // --- Scoreboard (left column, always visible) ---
            var board = MakePanel<Panel>("ScoreboardPanel", canvas.transform, new Vector2(380, 960), Vector2.zero, rounded, config);
            var boardRt = board.GetComponent<RectTransform>();
            boardRt.anchorMin = boardRt.anchorMax = new Vector2(0f, 0.5f);
            boardRt.pivot = new Vector2(0f, 0.5f);
            boardRt.anchoredPosition = new Vector2(40f, -10f);
            PlaceTop(MakeText("BoardTitle", board.transform, "SCOREBOARD", 26, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold).rectTransform, 28, new Vector2(320, 40));
            var boardRows = MakeRowsContainer("Rows", board.transform, 8f, false);
            PlaceTop(boardRows, 84, new Vector2(340, 840));
            var scoreboard = board.gameObject.AddComponent<ScoreboardUI>();
            SetPrivate(scoreboard, "rowsContainer", boardRows);
            SetPrivate(scoreboard, "rowPrefab", playerRowPrefab.GetComponent<PlayerRowView>());

            // --- Round panel ---
            // Layout order is deliberate: question, then what each choice actually does,
            // then the buttons. The buttons carry only the word you're choosing.
            var round = MakePanel<RoundPanel>("RoundPanel", canvas.transform, new Vector2(1040, 740), new Vector2(120, 0), rounded, config);
            var rTitle = MakeText("RoundTitle", round.transform, "THE BUTTON", 30, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(rTitle.rectTransform, 26, new Vector2(940, 38));
            var tie = MakeText("Tie", round.transform, "TIE. ONE MORE.", 20, Palette.Red, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(tie.rectTransform, 8, new Vector2(300, 28)).anchoredPosition = new Vector2(320f, -14f);
            tie.gameObject.SetActive(false);
            var prompt = MakeText("Prompt", round.transform, "Everyone presses green?", 42, Palette.Text, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(prompt.rectTransform, 68, new Vector2(920, 96));

            var rules = MakeText("Rules", round.transform, "", 21, Palette.Text, TextAlignmentOptions.TopLeft);
            rules.lineSpacing = 14f;
            PlaceTop(rules.rectTransform, 172, new Vector2(900, 118));

            var sub = MakeText("SubLine", round.transform, "", 19, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(sub.rectTransform, 294, new Vector2(920, 34));

            var buttonA = MakeAnswerButton("ButtonA", round.transform, new Vector2(440, 200), rounded, 48);
            PlaceTop(buttonA.GetComponent<RectTransform>(), 336, new Vector2(440, 200)).anchoredPosition = new Vector2(-236f, -336f);
            var buttonB = MakeAnswerButton("ButtonB", round.transform, new Vector2(440, 200), rounded, 48);
            PlaceTop(buttonB.GetComponent<RectTransform>(), 336, new Vector2(440, 200)).anchoredPosition = new Vector2(236f, -336f);

            var grid = new GameObject("NumberGrid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(round.transform, false);
            PlaceTop(grid, 336, new Vector2(700, 190));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(120, 82);
            gl.spacing = new Vector2(14, 14);
            gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gl.startAxis = GridLayoutGroup.Axis.Horizontal;
            gl.childAlignment = TextAnchor.UpperCenter;
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 5;
            var numberButtons = new Object[11];
            for (int v = 1; v <= 10; v++)
                numberButtons[v] = MakeAnswerButton("Num" + v, grid, new Vector2(120, 82), rounded, 34);
            numberButtons[0] = MakeAnswerButton("Num0", grid, new Vector2(120, 82), rounded, 34);

            var lockedHint = MakeText("LockedHint", round.transform, "Locked in. No takebacks.", 20, Palette.LockedIn, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(lockedHint.rectTransform, 596, new Vector2(900, 30));
            lockedHint.gameObject.SetActive(false);

            var countdown = MakeCountdown("Countdown", round.transform, rounded);
            PlaceTop(countdown.GetComponent<RectTransform>(), 690, new Vector2(920, 18));

            SetPrivate(round, "titleText", rTitle);
            SetPrivate(round, "promptText", prompt);
            SetPrivate(round, "rulesText", rules);
            SetPrivate(round, "subLineText", sub);
            SetPrivate(round, "tieText", tie);
            SetPrivate(round, "lockedHint", lockedHint);
            SetPrivate(round, "buttonA", buttonA);
            SetPrivate(round, "buttonB", buttonB);
            SetPrivate(round, "numberGrid", grid);
            SetPrivateArray(round, "numberButtons", numberButtons);
            SetPrivate(round, "countdown", countdown);

            // --- Minigame stage ---
            // Same panel size, same chrome positions as the round panel, so switching
            // between a question and a minigame doesn't move the furniture. Everything in
            // the middle is drawn at runtime by the minigame itself, through UiKit.
            var stage = MakePanel<MinigameStage>("MinigameStage", canvas.transform, new Vector2(1040, 740), new Vector2(120, 0), rounded, config);
            var mTitle = MakeText("StageTitle", stage.transform, "GREEN LIGHT", 30, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(mTitle.rectTransform, 26, new Vector2(940, 38));
            var mLevel = MakeText("Level", stage.transform, "LEVEL 1", 22, Palette.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            PlaceTop(mLevel.rectTransform, 28, new Vector2(240, 32)).anchoredPosition = new Vector2(-380f, -28f);
            var mAlive = MakeText("Alive", stage.transform, "4 LEFT", 22, Palette.TextDim, TextAlignmentOptions.TopRight, FontStyles.Bold);
            PlaceTop(mAlive.rectTransform, 28, new Vector2(240, 32)).anchoredPosition = new Vector2(380f, -28f);
            var mRule = MakeText("StageRule", stage.transform, "", 22, Palette.Text, TextAlignmentOptions.Top);
            PlaceTop(mRule.rectTransform, 74, new Vector2(900, 64));

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(stage.transform, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(940, 440);
            content.anchoredPosition = new Vector2(0f, -10f);

            var mBig = MakeText("BigCount", stage.transform, "", 120, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            var bigRt = mBig.rectTransform;
            bigRt.anchorMin = bigRt.anchorMax = bigRt.pivot = new Vector2(0.5f, 0.5f);
            bigRt.sizeDelta = new Vector2(400, 160);
            bigRt.anchoredPosition = new Vector2(0f, -20f);

            var mStatus = MakeText("Status", stage.transform, "", 24, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(mStatus.rectTransform, 618, new Vector2(900, 34));
            var mControls = MakeText("Controls", stage.transform, "SPACE", 20, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(mControls.rectTransform, 654, new Vector2(900, 28));
            var mCountdown = MakeCountdown("StageCountdown", stage.transform, rounded);
            PlaceTop(mCountdown.GetComponent<RectTransform>(), 690, new Vector2(920, 18));

            SetPrivate(stage, "titleText", mTitle);
            SetPrivate(stage, "ruleText", mRule);
            SetPrivate(stage, "levelText", mLevel);
            SetPrivate(stage, "aliveText", mAlive);
            SetPrivate(stage, "statusText", mStatus);
            SetPrivate(stage, "controlsText", mControls);
            SetPrivate(stage, "bigCountText", mBig);
            SetPrivate(stage, "contentArea", content);
            SetPrivate(stage, "countdown", mCountdown);
            SetPrivate(stage, "panelSprite", rounded);

            // --- Reveal panel ---
            var reveal = MakePanel<RevealPanel>("RevealPanel", canvas.transform, new Vector2(1040, 740), new Vector2(120, 0), rounded, config);
            var rvTitle = MakeText("RevealTitle", reveal.transform, "REVEAL", 30, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(rvTitle.rectTransform, 30, new Vector2(940, 40));
            var rvRows = MakeRowsContainer("Rows", reveal.transform, 8f, false);
            PlaceTop(rvRows, 90, new Vector2(820, 486));
            var reaction = MakeText("Reaction", reveal.transform, "", 26, Palette.TextDim, TextAlignmentOptions.Bottom, FontStyles.Italic);
            PlaceTop(reaction.rectTransform, 596, new Vector2(900, 90));
            SetPrivate(reveal, "titleText", rvTitle);
            SetPrivate(reveal, "rowsContainer", rvRows);
            SetPrivate(reveal, "rowPrefab", revealRowPrefab.GetComponent<RevealRowView>());
            SetPrivate(reveal, "reactionText", reaction);

            // --- Winner panel ---
            var winner = MakePanel<WinnerPanel>("WinnerPanel", canvas.transform, new Vector2(1040, 740), new Vector2(120, 0), rounded, config);
            var wLabel = MakeText("WinnerLabel", winner.transform, "SMARTEST IN THE GROUP", 40, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(wLabel.rectTransform, 70, new Vector2(940, 60));
            var wName = MakeText("WinnerName", winner.transform, "—", 84, Palette.Text, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(wName.rectTransform, 140, new Vector2(940, 110));
            var standings = MakeText("Standings", winner.transform, "", 24, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(standings.rectTransform, 280, new Vector2(700, 260));
            var backBtn = MakeButton("BackToLobbyButton", winner.transform, "BACK TO LOBBY", new Vector2(420, 76), Palette.Accent, Palette.TextOnAccent, rounded);
            PlaceTop(backBtn.GetComponent<RectTransform>(), 590, new Vector2(420, 76));
            var waiting = MakeText("Waiting", winner.transform, "Waiting for host…", 22, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(waiting.rectTransform, 610, new Vector2(700, 40));
            SetPrivate(winner, "labelText", wLabel);
            SetPrivate(winner, "nameText", wName);
            SetPrivate(winner, "standingsText", standings);
            SetPrivate(winner, "backButton", backBtn);
            SetPrivate(winner, "waitingText", waiting);

            // --- Sound settings (built last so it sits above every other panel) ---
            BuildSettingsPanel(canvas.transform, rounded, config);

            // --- Controller ---
            var ui = new GameObject("GameController").AddComponent<GameUI>();
            SetPrivate(ui, "scoreboardPanel", board);
            SetPrivate(ui, "scoreboard", scoreboard);
            SetPrivate(ui, "roundPanel", round);
            SetPrivate(ui, "minigameStage", stage);
            SetPrivate(ui, "revealPanel", reveal);
            SetPrivate(ui, "winnerPanel", winner);
            SetPrivate(ui, "roundCounter", counter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // ------------------------------------------------------------------
        // UI construction helpers
        // ------------------------------------------------------------------

        private static void AddCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.orthographic = true;
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void CreateBackground(Transform canvas)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var img = go.AddComponent<Image>();
            img.color = Palette.Background;
            img.raycastTarget = false;
            Stretch(go.GetComponent<RectTransform>());
        }

        private static T MakePanel<T>(string name, Transform parent, Vector2 size, Vector2 centerOffset,
            Sprite sprite, GameConfig config) where T : Panel
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<CanvasGroup>();
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Palette.Panel;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = centerOffset;
            var panel = go.AddComponent<T>();
            panel.ApplyConfig(config);
            return panel;
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        private static Button MakeButton(string name, Transform parent, string label, Vector2 size,
            Color bg, Color textColor, Sprite sprite, float fontSize = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bg;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            float fs = fontSize > 0f ? fontSize : (size.y > 120 ? 40 : 26);
            var text = MakeText("Label", go.transform, label, fs, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform, 10, 8, 10, 8);
            return btn;
        }

        private static AnswerButton MakeAnswerButton(string name, Transform parent, Vector2 size, Sprite sprite, float fontSize)
        {
            var btn = MakeButton(name, parent, "", size, Palette.Neutral, Palette.Text, sprite, fontSize);
            var go = btn.gameObject;
            var group = go.AddComponent<CanvasGroup>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();

            var check = MakeText("Check", go.transform, "✓", Mathf.Max(26f, fontSize * 0.8f), Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            var chk = check.rectTransform;
            chk.anchorMin = chk.anchorMax = new Vector2(1f, 1f);
            chk.pivot = new Vector2(1f, 1f);
            chk.sizeDelta = new Vector2(44, 44);
            chk.anchoredPosition = new Vector2(-6f, -4f);
            check.gameObject.SetActive(false);

            var ab = go.AddComponent<AnswerButton>();
            SetPrivate(ab, "button", btn);
            SetPrivate(ab, "background", go.GetComponent<Image>());
            SetPrivate(ab, "label", label);
            SetPrivate(ab, "checkMark", check);
            SetPrivate(ab, "group", group);
            return ab;
        }

        /// <summary>A standard Unity slider, built from the same rounded sprite as everything else.</summary>
        private static Slider MakeSlider(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = sprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Palette.Neutral;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 14f);
            bgRt.anchoredPosition = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.5f);
            faRt.anchorMax = new Vector2(1f, 0.5f);
            faRt.pivot = new Vector2(0.5f, 0.5f);
            faRt.sizeDelta = new Vector2(-24f, 14f);
            faRt.anchoredPosition = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = sprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Palette.Accent;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = new Vector2(12f, 0f);
            fillRt.anchoredPosition = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.sizeDelta = new Vector2(-24f, 0f);
            haRt.anchoredPosition = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = sprite;
            handleImg.type = Image.Type.Sliced;
            handleImg.color = Palette.Text;
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(24f, 36f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }

        /// <summary>
        /// The sound settings, plus the small button that opens them. Identical in both
        /// scenes — the panel is built last so it sits on top of everything else.
        /// </summary>
        private static SettingsPanel BuildSettingsPanel(Transform canvas, Sprite rounded, GameConfig config)
        {
            var open = MakeButton("SoundButton", canvas, "SOUND", new Vector2(132, 52),
                Palette.PanelRaised, Palette.TextDim, rounded, 20);
            var openRt = open.GetComponent<RectTransform>();
            openRt.anchorMin = openRt.anchorMax = openRt.pivot = new Vector2(1f, 1f);
            openRt.anchoredPosition = new Vector2(-28f, -20f);

            var panel = MakePanel<SettingsPanel>("SettingsPanel", canvas, new Vector2(620, 560), Vector2.zero, rounded, config);
            var title = MakeText("SettingsTitle", panel.transform, "SOUND", 30, Palette.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            PlaceTop(title.rectTransform, 28, new Vector2(520, 40));

            string[] labels = { "EVERYTHING", "HOST VOICE", "MUSIC", "SOUND EFFECTS" };
            var sliders = new Slider[4];
            var values = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 96f + i * 92f;
                var label = MakeText("Label" + i, panel.transform, labels[i], 20, Palette.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                PlaceTop(label.rectTransform, y, new Vector2(500, 28));
                values[i] = MakeText("Value" + i, panel.transform, "100%", 20, Palette.Text, TextAlignmentOptions.TopRight, FontStyles.Bold);
                PlaceTop(values[i].rectTransform, y, new Vector2(500, 28));
                sliders[i] = MakeSlider("Slider" + i, panel.transform, rounded, new Vector2(500, 38));
                PlaceTop(sliders[i].GetComponent<RectTransform>(), y + 30f, new Vector2(500, 38));
            }

            var hint = MakeText("MusicHint", panel.transform, "No music yet — drop a loop into Audio/Music and rebuild.",
                16, Palette.TextDim, TextAlignmentOptions.Top);
            PlaceTop(hint.rectTransform, 450, new Vector2(540, 28));

            var close = MakeButton("CloseSettings", panel.transform, "DONE", new Vector2(240, 66),
                Palette.Accent, Palette.TextOnAccent, rounded);
            PlaceTop(close.GetComponent<RectTransform>(), 478, new Vector2(240, 66));

            SetPrivate(panel, "masterSlider", sliders[0]);
            SetPrivate(panel, "narratorSlider", sliders[1]);
            SetPrivate(panel, "musicSlider", sliders[2]);
            SetPrivate(panel, "sfxSlider", sliders[3]);
            SetPrivate(panel, "masterValue", values[0]);
            SetPrivate(panel, "narratorValue", values[1]);
            SetPrivate(panel, "musicValue", values[2]);
            SetPrivate(panel, "sfxValue", values[3]);
            SetPrivate(panel, "musicHint", hint);
            SetPrivate(panel, "closeButton", close);
            SetPrivate(panel, "openButton", open);
            return panel;
        }

        private static CountdownBar MakeCountdown(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var track = go.AddComponent<Image>();
            track.sprite = sprite;
            track.type = Image.Type.Sliced;
            track.color = Palette.PanelRaised;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Sliced;
            fill.color = Palette.TextDim;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var numeric = MakeText("Numeric", go.transform, "", 30, Palette.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            var nRt = numeric.rectTransform;
            nRt.anchorMin = nRt.anchorMax = new Vector2(0.5f, 1f);
            nRt.pivot = new Vector2(0.5f, 0f);
            nRt.sizeDelta = new Vector2(120, 40);
            nRt.anchoredPosition = new Vector2(0f, 8f);

            var bar = go.AddComponent<CountdownBar>();
            SetPrivate(bar, "fill", fillRt);
            SetPrivate(bar, "fillImage", fill);
            SetPrivate(bar, "numeric", numeric);
            return bar;
        }

        private static TMP_InputField MakeInputField(string name, Transform parent, string placeholder, int charLimit,
            TMP_InputField.ContentType contentType, Sprite sprite, float fontSize = 26f, bool centered = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = Palette.PanelRaised;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;

            var area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var areaRt = area.GetComponent<RectTransform>();
            Stretch(areaRt, 20, 8, 20, 8);
            area.AddComponent<RectMask2D>();

            var align = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            var ph = MakeText("Placeholder", area.transform, placeholder, fontSize, Palette.TextDim, align, FontStyles.Italic);
            Stretch(ph.rectTransform);
            var txt = MakeText("Text", area.transform, string.Empty, fontSize, Palette.Text, align);
            Stretch(txt.rectTransform);
            txt.richText = false;
            if (centered) txt.characterSpacing = 12;

            input.textViewport = areaRt;
            input.textComponent = txt;
            input.placeholder = ph;
            input.characterLimit = charLimit;
            input.contentType = contentType;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.pointSize = fontSize;
            input.customCaretColor = true;
            input.caretColor = Palette.Accent;
            var sel = Palette.Accent;
            input.selectionColor = new Color(sel.r, sel.g, sel.b, 0.35f);

            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            input.colors = colors;
            return input;
        }

        /// <summary>Container for row instances. With a layout group (lobby) or manual positions (scoreboard/reveal).</summary>
        private static RectTransform MakeRowsContainer(string name, Transform parent, float spacing, bool layoutGroup)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            if (layoutGroup)
            {
                var v = go.AddComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(0, 0, 0, 0);
                v.spacing = spacing;
                v.childAlignment = TextAnchor.UpperCenter;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }
            return rt;
        }

        // ------------------------------------------------------------------
        // Layout / reflection utilities
        // ------------------------------------------------------------------

        private static RectTransform PlaceTop(RectTransform rt, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, -y);
            return rt;
        }

        private static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        private static void SetPrivate(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[Smartest] Could not find serialized field '{field}' on {target.GetType().Name}.");
            }
        }

        private static void SetPrivateArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[Smartest] Could not find serialized array '{field}' on {target.GetType().Name}.");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
