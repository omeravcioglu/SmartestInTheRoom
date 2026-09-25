using System.Collections.Generic;
using System.IO;
using System.Text;
using Smartest.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Smartest.EditorTools
{
    /// <summary>
    /// Tools &gt; Smartest &gt; Voice Studio.
    ///
    /// Records the whole host script with ElevenLabs. The API key is stored in this machine's
    /// EditorPrefs and never written into the project, so it can't end up in a build or in a
    /// folder you share. The only place it is ever sent is api.elevenlabs.io.
    ///
    /// Clips land in Audio/Voice as &lt;key&gt;_1.mp3, &lt;key&gt;_2.mp3 ... and Build Scenes wires
    /// them to the matching line. Anything not generated yet simply stays silent in game.
    /// </summary>
    public class VoiceStudioWindow : EditorWindow
    {
        private const string PrefKey = "Smartest.ElevenLabs.ApiKey";
        private const string PrefVoice = "Smartest.ElevenLabs.VoiceId";
        private const string PrefModel = "Smartest.ElevenLabs.Model";
        private const string PrefStability = "Smartest.ElevenLabs.Stability";
        private const string PrefSimilarity = "Smartest.ElevenLabs.Similarity";
        private const string PrefStyle = "Smartest.ElevenLabs.Style";

        private const string DefaultModel = "eleven_multilingual_v2";

        private string _apiKey = "";
        private string _voiceId = "";
        private string _model = DefaultModel;
        private float _stability = 0.40f;
        private float _similarity = 0.80f;
        private float _style = 0.35f;

        private Vector2 _scroll;
        private readonly List<Job> _queue = new List<Job>();
        private UnityWebRequest _inFlight;
        private Job _current;
        private int _done;
        private int _failed;
        private string _status = "";
        private bool _needsRefresh = true;
        private List<VoiceScript.Entry> _script;
        private int _haveClips;
        private int _totalClips;

        private struct Job
        {
            public string Key;
            public int Variant;
            public string Text;
        }

        [MenuItem("Tools/Smartest/Voice Studio")]
        public static void Open()
        {
            var w = GetWindow<VoiceStudioWindow>(false, "Voice Studio", true);
            w.minSize = new Vector2(560f, 420f);
            w.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(PrefKey, "");
            _voiceId = EditorPrefs.GetString(PrefVoice, "");
            _model = EditorPrefs.GetString(PrefModel, DefaultModel);
            _stability = EditorPrefs.GetFloat(PrefStability, 0.40f);
            _similarity = EditorPrefs.GetFloat(PrefSimilarity, 0.80f);
            _style = EditorPrefs.GetFloat(PrefStyle, 0.35f);
            _needsRefresh = true;
        }

        private void RefreshCounts()
        {
            _script = VoiceScript.All();
            _haveClips = 0;
            _totalClips = 0;
            foreach (var e in _script)
            {
                int n = e.Texts != null ? e.Texts.Length : 0;
                _totalClips += n;
                for (int i = 0; i < n; i++)
                    if (VoicePaths.Find(e.Key, i) != null) _haveClips++;
            }
            _needsRefresh = false;
        }

        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (_needsRefresh) RefreshCounts();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ElevenLabs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Your API key is saved on this computer only (EditorPrefs). It never goes into the " +
                "project folder, a build, or anywhere except api.elevenlabs.io.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            _apiKey = EditorGUILayout.PasswordField("API key", _apiKey);
            _voiceId = EditorGUILayout.TextField("Voice ID", _voiceId);
            _model = EditorGUILayout.TextField("Model", _model);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefKey, _apiKey);
                EditorPrefs.SetString(PrefVoice, _voiceId);
                EditorPrefs.SetString(PrefModel, _model);
            }

            EditorGUI.BeginChangeCheck();
            _stability = EditorGUILayout.Slider("Stability", _stability, 0f, 1f);
            _similarity = EditorGUILayout.Slider("Similarity", _similarity, 0f, 1f);
            _style = EditorGUILayout.Slider("Style", _style, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(PrefStability, _stability);
                EditorPrefs.SetFloat(PrefSimilarity, _similarity);
                EditorPrefs.SetFloat(PrefStyle, _style);
            }
            EditorGUILayout.LabelField(" ",
                "Lower stability = more expressive. This host wants roughly 0.4 / 0.8 / 0.35.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            int chars = VoiceScript.CharacterCount();
            EditorGUILayout.LabelField("Script",
                $"{_script.Count} moments · {_totalClips} clips · {_haveClips} recorded · {chars:N0} characters");

            EditorGUILayout.Space(4);
            DrawActions();
            EditorGUILayout.Space(8);
            DrawList();
        }

        private void DrawActions()
        {
            bool busy = _inFlight != null || _queue.Count > 0;
            bool ready = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_voiceId);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(busy || !ready))
                {
                    if (GUILayout.Button($"Record missing ({_totalClips - _haveClips})", GUILayout.Height(26)))
                        QueueAll(onlyMissing: true);
                    if (GUILayout.Button("Re-record everything", GUILayout.Height(26)) &&
                        EditorUtility.DisplayDialog("Re-record everything?",
                            $"This overwrites all {_totalClips} clips and spends roughly " +
                            $"{VoiceScript.CharacterCount():N0} characters of your ElevenLabs quota.",
                            "Record", "Cancel"))
                        QueueAll(onlyMissing: false);
                }
                using (new EditorGUI.DisabledScope(!busy))
                {
                    if (GUILayout.Button("Stop", GUILayout.Height(26), GUILayout.Width(70))) Cancel();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh")) _needsRefresh = true;
                if (GUILayout.Button("Wire clips into the game"))
                {
                    var wiring = VoiceWiring.Rewire();
                    AssetDatabase.SaveAssets();
                    _status = wiring.Message;
                    _needsRefresh = true;
                }
                if (GUILayout.Button("Export script as text")) ExportScript();
                if (GUILayout.Button("Open voice folder"))
                    EditorUtility.RevealInFinder(VoicePaths.VoiceDir);
            }

            if (!ready)
                EditorGUILayout.HelpBox("Paste an API key and a voice ID to start recording.", MessageType.Info);

            if (busy)
            {
                int total = _done + _failed + _queue.Count + (_inFlight != null ? 1 : 0);
                var rect = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(rect, total > 0 ? (float)(_done + _failed) / total : 0f,
                    $"{_done + _failed} / {total}   {_status}");
                Repaint();
            }
            else if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, _failed > 0 ? MessageType.Warning : MessageType.Info);
            }
        }

        private void DrawList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _script)
            {
                EditorGUILayout.LabelField(entry.Key, EditorStyles.boldLabel);
                for (int i = 0; i < entry.Texts.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string path = VoicePaths.Find(entry.Key, i);
                        GUILayout.Label(path != null ? "●" : "○", GUILayout.Width(14));
                        EditorGUILayout.LabelField($"{i + 1}. {entry.Texts[i]}", EditorStyles.wordWrappedMiniLabel);

                        using (new EditorGUI.DisabledScope(path == null))
                        {
                            if (GUILayout.Button("Play", GUILayout.Width(46)))
                                PlayPreview(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                        }
                        using (new EditorGUI.DisabledScope(_inFlight != null || string.IsNullOrEmpty(_apiKey)))
                        {
                            if (GUILayout.Button(path == null ? "Record" : "Redo", GUILayout.Width(58)))
                            {
                                _queue.Add(new Job { Key = entry.Key, Variant = i, Text = entry.Texts[i] });
                                _done = 0; _failed = 0;
                            }
                        }
                    }
                }
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------

        private void QueueAll(bool onlyMissing)
        {
            _queue.Clear();
            _done = 0;
            _failed = 0;
            foreach (var entry in _script)
            {
                for (int i = 0; i < entry.Texts.Length; i++)
                {
                    if (onlyMissing && VoicePaths.Find(entry.Key, i) != null) continue;
                    _queue.Add(new Job { Key = entry.Key, Variant = i, Text = entry.Texts[i] });
                }
            }
            _status = _queue.Count == 0 ? "Everything is already recorded." : "Starting…";
        }

        private void Cancel()
        {
            _queue.Clear();
            if (_inFlight != null)
            {
                _inFlight.Abort();
                _inFlight.Dispose();
                _inFlight = null;
            }
            _status = $"Stopped. {_done} recorded, {_failed} failed.";
            AssetDatabase.Refresh();
            _needsRefresh = true;
        }

        private void Update()
        {
            if (_inFlight == null)
            {
                if (_queue.Count == 0) return;
                _current = _queue[0];
                _queue.RemoveAt(0);
                _inFlight = BuildRequest(_current);
                _status = $"{_current.Key} #{_current.Variant + 1}";
                if (_inFlight == null) { _failed++; return; }
                _inFlight.SendWebRequest();
                return;
            }

            if (!_inFlight.isDone) return;

            bool ok = _inFlight.result == UnityWebRequest.Result.Success;
            if (ok)
            {
                var bytes = _inFlight.downloadHandler.data;
                if (bytes != null && bytes.Length > 512)
                {
                    string path = VoicePaths.Mp3(_current.Key, _current.Variant);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                    _done++;
                }
                else
                {
                    Debug.LogError($"[Voice Studio] {_current.Key} #{_current.Variant + 1}: empty response.");
                    _failed++;
                }
            }
            else
            {
                string body = _inFlight.downloadHandler != null ? _inFlight.downloadHandler.text : "";
                Debug.LogError($"[Voice Studio] {_current.Key} #{_current.Variant + 1} failed " +
                               $"({_inFlight.responseCode}): {_inFlight.error} {Trim(body)}");
                _failed++;
                // A bad key or an exhausted quota will fail every remaining line the same way.
                if (_inFlight.responseCode == 401 || _inFlight.responseCode == 403)
                {
                    _queue.Clear();
                    _status = "Stopped: ElevenLabs rejected the API key.";
                }
                else if (_inFlight.responseCode == 429)
                {
                    _queue.Clear();
                    _status = "Stopped: ElevenLabs rate limit or quota reached.";
                }
            }

            _inFlight.Dispose();
            _inFlight = null;

            if (_queue.Count == 0)
            {
                AssetDatabase.Refresh();
                _needsRefresh = true;
                // Files on disk aren't in the game until they're on the Bootstrap prefab,
                // so do that here rather than leaving it as a step to remember.
                var wiring = VoiceWiring.Rewire();
                AssetDatabase.SaveAssets();
                if (!_status.StartsWith("Stopped"))
                    _status = $"Done. {_done} recorded" +
                              (_failed > 0 ? $", {_failed} failed — see the Console. " : ". ") +
                              wiring.Message;
            }
            Repaint();
        }

        private UnityWebRequest BuildRequest(Job job)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_voiceId)) return null;

            string url = $"https://api.elevenlabs.io/v1/text-to-speech/{_voiceId}";
            string body =
                "{\"text\":\"" + Escape(job.Text) + "\"," +
                "\"model_id\":\"" + Escape(string.IsNullOrEmpty(_model) ? DefaultModel : _model) + "\"," +
                "\"voice_settings\":{" +
                "\"stability\":" + _stability.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                "\"similarity_boost\":" + _similarity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                "\"style\":" + _style.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                "\"use_speaker_boost\":true}}";

            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 60
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/mpeg");
            request.SetRequestHeader("xi-api-key", _apiKey);
            return request;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= 300 ? s : s.Substring(0, 300) + "…";
        }

        private static void PlayPreview(AudioClip clip)
        {
            if (clip == null) return;
            // AudioUtil is internal, so this is the supported-enough way to audition in-editor.
            var type = typeof(EditorApplication).Assembly.GetType("UnityEditor.AudioUtil");
            var method = type?.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (method != null) method.Invoke(null, new object[] { clip, 0, false });
            else Debug.Log($"[Voice Studio] Select {clip.name} in the Project window to hear it.");
        }

        private void ExportScript()
        {
            string path = EditorUtility.SaveFilePanel("Export voice script", "", "smartest_voice_script.txt", "txt");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("SMARTEST IN THE ROOM — host script");
            sb.AppendLine($"{VoiceScript.ClipCount()} clips, {VoiceScript.CharacterCount():N0} characters");
            sb.AppendLine("Voice: a friend on the couch who is way too into this. Casual, loud, never cruel.");
            sb.AppendLine();
            foreach (var entry in _script)
            {
                sb.AppendLine(entry.Key);
                for (int i = 0; i < entry.Texts.Length; i++)
                    sb.AppendLine($"  {i + 1}. {entry.Texts[i]}");
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);
        }
    }
}
