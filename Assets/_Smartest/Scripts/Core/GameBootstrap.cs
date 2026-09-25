using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// The one persistent object in the game. It is NOT placed in any scene: it is
    /// instantiated automatically from Resources/Bootstrap.prefab before the first
    /// scene loads, so returning to the Menu scene never creates duplicates.
    /// Holds the GameConfig and carries the Netcode NetworkManager + NetSession
    /// (same prefab) across the Menu -> Game scene load.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private const string ResourceName = "Bootstrap";

        [SerializeField] private GameConfig config;

        public static GameBootstrap Instance { get; private set; }
        public GameConfig Config => config;

        /// <summary>Convenience accessor; never null once the game is running.</summary>
        public static GameConfig ConfigOrDefault
        {
            get
            {
                if (Instance != null && Instance.config != null) return Instance.config;
                return _fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<GameConfig>());
            }
        }
        private static GameConfig _fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return; // Unity's null check also covers destroyed objects
            var prefab = Resources.Load<GameObject>(ResourceName);
            if (prefab == null)
            {
                Debug.LogError("[Smartest] Resources/Bootstrap.prefab is missing. Run Tools > Smartest > Build Scenes.");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "Bootstrap";
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
