using System.Collections.Generic;
using System.Text;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>Final screen: winner, standings, Back to lobby (host) / Waiting (clients).</summary>
    public class WinnerPanel : Panel
    {
        [Header("Winner")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text standingsText;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text waitingText;

        protected override void Awake()
        {
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        public void ShowWinner(PlayerData winner, IReadOnlyList<PlayerData> players, bool isHost)
        {
            if (labelText != null) labelText.text = "SMARTEST IN THE GROUP";
            if (nameText != null) nameText.text = winner != null ? winner.DisplayName : "—";

            var sorted = new List<PlayerData>(players);
            sorted.Sort((a, b) =>
            {
                int byScore = b.Score.Value.CompareTo(a.Score.Value);
                return byScore != 0 ? byScore : a.OwnerClientId.CompareTo(b.OwnerClientId);
            });
            var sb = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(i + 1).Append(". ").Append(sorted[i].DisplayName).Append(" — ").Append(sorted[i].Score.Value);
            }
            if (standingsText != null) standingsText.text = sb.ToString();

            if (backButton != null) backButton.gameObject.SetActive(isHost);
            if (waitingText != null) waitingText.gameObject.SetActive(!isHost);
        }

        private void OnBack()
        {
            if (NetSession.Instance != null && NetSession.Instance.IsHost)
                NetSession.Instance.ReturnToLobby();
        }
    }
}
