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
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Smartest.EditorTools
{
    /// <summary>
    /// Tools > Smartest > Build Scenes.
    ///
    ///  - folder layout, GameConfig asset
    ///  - the Tabloid art: every shape in InkSprites baked to Resources/Ink as a sprite
    ///  - fonts: TextMeshPro assets for whatever TTFs are in Assets/_Smartest/Fonts, listed in
    ///    Resources/SmartestFonts (missing faces fall back to TMP's default font)
    ///  - one RoundDefinition asset per challenge (Data/Rounds) from RoundCatalog + MinigameRegistry,
    ///    listed in Resources/RoundLibrary
    ///  - Prefabs: Resources/Bootstrap (GameBootstrap + NetworkManager + UnityTransport + NetSession + VoiceLines),
    ///             Resources/GameState (spawned by the host once everyone has loaded Game),
    ///             Prefabs/PlayerData (NetworkObject + PlayerData)
    ///  - Menu.unity: front page / join card / lobby / sound, fully wired
    ///  - Game.unity: masthead, seat rail, host caption, round / minigame / reveal stage, winner page
    /// Re-running is safe: assets are updated in place (GUIDs stay stable), scenes are rebuilt.
    ///
    /// Without the editor open:
    ///   Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod Smartest.EditorTools.SceneBuilder.BuildScenesBatch -quit
    /// </summary>
    public static class SceneBuilder
    {
        private const string Root = "Assets/_Smartest";
        private const string ScenesDir = Root + "/Scenes";
        private const string MenuScenePath = ScenesDir + "/Menu.unity";
        private const string GameScenePath = ScenesDir + "/Game.unity";
        private const string ConfigPath = Root + "/Data/GameConfig.asset";
        private const string LibraryPath = Root + "/Resources/RoundLibrary.asset"; // loadable by name at runtime
        private const string RoundsDir = Root + "/Data/Rounds";
        private const string VoiceDir = Root + "/Audio/Voice";
        private const string InkDir = Root + "/Resources/" + InkSprites.Folder;
        private const string FontsDir = Root + "/Fonts";
        private const string FontAssetsDir = FontsDir + "/SDF";
        private const string FontSetPath = Root + "/Resources/" + Typo.ResourceName + ".asset";
        private const string BootstrapPrefabPath = Root + "/Resources/Bootstrap.prefab";
        private const string GameStatePrefabPath = Root + "/Resources/GameState.prefab";
        private const string PlayerDataPrefabPath = Root + "/Prefabs/PlayerData.prefab";
        private const string DefaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        private const float RefW = 1920f;
        private const float RefH = 1080f;

        [MenuItem("Tools/Smartest/Build Scenes")]
        public static void BuildScenes()
        {
            if (!TmpReady())
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
                    "Menu.unity / Game.unity already exist under _Smartest/Scenes.\n\nRebuild them (prefabs, art, fonts and round assets are updated in place)?",
                    "Rebuild", "Cancel");
                if (!ok) return;
            }

            Build();
            EditorSceneManager.OpenScene(MenuScenePath);
        }

        /// <summary>The same build with no dialogs, for -batchmode -executeMethod.</summary>
        public static void BuildScenesBatch()
        {
            try
            {
                if (!TmpReady())
                {
                    Debug.LogError("[Smartest] TMP Essential Resources are missing; import them once from the editor.");
                    EditorApplication.Exit(2);
                    return;
                }
                Build();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static bool TmpReady() => Resources.Load<TMP_Settings>("TMP Settings") != null;

        private static void Build()
        {
            EnsureFolders();
            BakeInkSprites();
            BuildFonts();
            GameConfig config = EnsureGameConfig();
            RoundLibrary library = BuildRoundAssets();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject playerDataPrefab = BuildPlayerDataPrefab();
            GameObject gameStatePrefab = BuildGameStatePrefab();
            BuildBootstrapPrefab(config, playerDataPrefab, gameStatePrefab);
            AssetDatabase.SaveAssets();

            BuildMenuScene(config);
            BuildGameScene(config);

            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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
                Root + "/Resources", InkDir, FontsDir, FontAssetsDir,
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

        private static string SysPath(string assetPath) =>
            Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

        // ------------------------------------------------------------------
        // Art: every InkSprites shape, baked to Resources/Ink
        // ------------------------------------------------------------------

        private static void BakeInkSprites()
        {
            var wanted = new List<(string path, Vector4 border, FilterMode filter)>();
            foreach (var baked in InkSprites.RenderCatalogue())
            {
                string path = $"{InkDir}/{baked.Name}.png";
                byte[] png = baked.Texture.EncodeToPNG();
                var filter = baked.Texture.filterMode;
                Object.DestroyImmediate(baked.Texture);

                string sys = SysPath(path);
                if (!File.Exists(sys) || !SameBytes(File.ReadAllBytes(sys), png))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(sys));
                    File.WriteAllBytes(sys, png);
                }
                wanted.Add((path, baked.Border, filter));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            int changed = 0;
            foreach (var (path, border, filter) in wanted)
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                bool same = ti.textureType == TextureImporterType.Sprite
                            && ti.spriteImportMode == SpriteImportMode.Single
                            && Mathf.Approximately(ti.spritePixelsPerUnit, InkSprites.PixelsPerUnit)
                            && !ti.mipmapEnabled && ti.alphaIsTransparency
                            && ti.filterMode == filter
                            && ti.textureCompression == TextureImporterCompression.Uncompressed
                            && settings.spriteBorder == border
                            && settings.spriteMeshType == SpriteMeshType.FullRect;
                if (same) continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = InkSprites.PixelsPerUnit;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = filter;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
                changed++;
            }
            InkSprites.ClearCache();
            Debug.Log($"[Smartest] Ink art: {wanted.Count} sprites ({changed} re-imported).");
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Fonts: TMP assets for the TTFs in Assets/_Smartest/Fonts
        // ------------------------------------------------------------------

        /// <summary>Static TTFs from Google Fonts (Archivo, Atkinson Hyperlegible Next and Mono).</summary>
        private static readonly (string slot, string file)[] FontFiles =
        {
            ("display", "Archivo_ExtraCondensed-Black"),
            ("sticker", "Archivo-Black"),
            ("names", "Archivo_Condensed-ExtraBold"),
            ("label", "Archivo_Expanded-ExtraBold"),
            ("body", "AtkinsonHyperlegibleNext-Regular"),
            ("bodyBold", "AtkinsonHyperlegibleNext-Bold"),
            ("bodyItalic", "AtkinsonHyperlegibleNext-Italic"),
            ("bodyBoldItalic", "AtkinsonHyperlegibleNext-BoldItalic"),
            ("mono", "AtkinsonHyperlegibleMono-ExtraBold"),
        };

        /// <summary>
        /// Every character the game puts on screen beyond plain ASCII. Each face is checked against
        /// it at build time, so a gap shows up in the build log rather than as a box in the game.
        /// (The glyphs aren't kept: TMP empties dynamic atlases when the editor quits and fills
        /// them again on demand from the TTF, Turkish letters included.)
        /// </summary>
        private const string BakedCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "çğıİöşüÇĞÖŞÜâîûÂÎÛéèêáàäëïôÉÈÁÀÄÖ" +
            "–—‘’“”…·−↑↓↔×▲►▼◄√";

        private static void BuildFonts()
        {
            var set = AssetDatabase.LoadAssetAtPath<FontSet>(FontSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FontSet>();
                AssetDatabase.CreateAsset(set, FontSetPath);
            }

            var faces = new Dictionary<string, TMP_FontAsset>();
            foreach (var (slot, file) in FontFiles)
            {
                var ttf = FindFont(file);
                if (ttf == null) continue;
                var asset = EnsureFontAsset(ttf, file);
                if (asset != null) faces[slot] = asset;
            }

            set.display = Face(faces, "display");
            set.sticker = Face(faces, "sticker");
            set.names = Face(faces, "names");
            set.label = Face(faces, "label");
            set.body = Face(faces, "body");
            set.mono = Face(faces, "mono");

            // Atkinson's bold and italic become the body face's <b> and <i>.
            if (set.body != null)
            {
                var table = set.body.fontWeightTable;
                if (table != null && table.Length > 7)
                {
                    table[4].italicTypeface = Face(faces, "bodyItalic");
                    table[7].regularTypeface = Face(faces, "bodyBold");
                    table[7].italicTypeface = Face(faces, "bodyBoldItalic");
                    EditorUtility.SetDirty(set.body);
                }
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Typo.Reload();

            if (faces.Count == 0)
                Debug.Log("[Smartest] No Tabloid fonts in Assets/_Smartest/Fonts yet — text uses TextMeshPro's default face. " +
                          "Drop the Archivo / Atkinson Hyperlegible TTFs there and rebuild.");
            else
                Debug.Log($"[Smartest] Fonts: {faces.Count}/{FontFiles.Length} faces found in {FontsDir}.");
        }

        private static TMP_FontAsset Face(Dictionary<string, TMP_FontAsset> faces, string slot)
            => faces.TryGetValue(slot, out var f) ? f : null;

        private static Font FindFont(string fileName)
        {
            if (!AssetDatabase.IsValidFolder(FontsDir)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { FontsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), fileName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Font>(path);
            }
            return null;
        }

        private static TMP_FontAsset EnsureFontAsset(Font font, string name)
        {
            string path = $"{FontAssetsDir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogWarning($"[Smartest] Couldn't make a TextMeshPro font from {name}.");
                return null;
            }
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, path);
            // The atlas and material live inside the font asset.
            if (asset.atlasTextures != null)
            {
                foreach (var tex in asset.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, asset);
                }
            }
            if (asset.material != null)
            {
                asset.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            asset.TryAddCharacters(BakedCharacters, out string missing); // a coverage check; see BakedCharacters
            if (!string.IsNullOrEmpty(missing))
                Debug.Log($"[Smartest] {name} has no glyph for: {missing} (TMP's fallback covers them).");
            var fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
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

        private static void AddScenesToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            // Keep other scenes of our own; drop the project template's SampleScene and the like,
            // which would otherwise ship in the build.
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == MenuScenePath || s.path == GameScenePath) continue;
                if (!s.path.StartsWith(Root + "/")) continue;
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

            // Three audio channels the player can set independently, and the one listener that
            // hears them (the scene cameras have none, and this object outlives both scenes).
            go.AddComponent<AudioListener>();
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

        // ------------------------------------------------------------------
        // Menu scene
        // ------------------------------------------------------------------

        private static void BuildMenuScene(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);
            var frame = CreateFrame(canvas.transform);

            var settings = SettingsPanel.Create(frame, config);
            var lobby = LobbyUI.Create(frame, config, settings);
            CountChallenges(out int social, out int minigames);
            MenuUI.Create(frame, config, settings, lobby, social, minigames);
            HostCaption.Create(frame, 48f, 930f, 1060f); // clear of the play card
            settings.transform.SetAsLastSibling(); // the modal sits above everything

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void CountChallenges(out int social, out int minigames)
        {
            social = 0;
            minigames = 0;
            foreach (var spec in RoundCatalog.AllChallenges())
            {
                if (spec.Kind == RoundKind.Minigame) minigames++;
                else social++;
            }
        }

        // ------------------------------------------------------------------
        // Game scene
        // ------------------------------------------------------------------

        private static void BuildGameScene(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);
            var frame = CreateFrame(canvas.transform);

            // (GameState is a network prefab spawned by the host after everyone loads this scene — see NetSession.)

            var settings = SettingsPanel.Create(frame, config);

            // Everything that stays up for the whole match, in one group the winner page can hide.
            var hudRoot = Ink.Node(frame, "Hud");
            hudRoot.Fill();
            var hud = hudRoot.gameObject.AddComponent<CanvasGroup>();

            var masthead = Masthead.Create(hudRoot, config, settings);

            // The stage: x 48–1872, y 150–762. The round, the minigame and the reveal take turns.
            var stage = Ink.Node(hudRoot, "Stage");
            stage.At(48f, 150f, RoundPanel.Width, RoundPanel.Height);
            var round = RoundPanel.Create(stage, config);
            var minigame = MinigameStage.Create(stage, config);
            var reveal = RevealPanel.Create(stage, config);

            var caption = HostCaption.Create(hudRoot, 48f, 774f);
            var rail = SeatRail.Create(hudRoot, 48f, 856f, masthead.Race);

            var winner = WinnerPanel.Create(frame, config, settings);
            settings.transform.SetAsLastSibling();

            var ui = new GameObject("GameController").AddComponent<GameUI>();
            SetPrivate(ui, "hud", hud);
            SetPrivate(ui, "masthead", masthead);
            SetPrivate(ui, "seatRail", rail);
            SetPrivate(ui, "hostCaption", caption);
            SetPrivate(ui, "roundPanel", round);
            SetPrivate(ui, "minigameStage", minigame);
            SetPrivate(ui, "revealPanel", reveal);
            SetPrivate(ui, "winnerPanel", winner);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // ------------------------------------------------------------------
        // Scene plumbing
        // ------------------------------------------------------------------

        private static void AddCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Paper;
            cam.orthographic = true;
        }

        /// <summary>
        /// Screen Space Overlay at 1920 × 1080, set to Expand: the canvas is never smaller than
        /// the design in either direction, so a 16:10 or ultrawide screen gets extra paper around
        /// the page instead of a page that runs off the edge.
        /// </summary>
        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>The 1920 × 1080 page every layout is written against, centred on the canvas.</summary>
        private static RectTransform CreateFrame(Transform canvas)
        {
            var rt = Ink.Node(canvas, "Frame");
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RefW, RefH);
            rt.anchoredPosition = Vector2.zero;
            return rt;
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
            var img = Ink.Node(canvas, "Background").gameObject.AddComponent<Image>();
            img.color = Palette.Paper;
            img.raycastTarget = false;
            img.rectTransform.Fill();
        }

        // ------------------------------------------------------------------
        // Reflection utilities
        // ------------------------------------------------------------------

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
