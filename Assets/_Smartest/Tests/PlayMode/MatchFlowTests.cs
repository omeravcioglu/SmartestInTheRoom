using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using Smartest.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// The host's whole loop on one machine, with no cloud involved: host a Local lobby,
    /// start, watch the engine spawn and deal round one, go back to the lobby, and start
    /// again. The second start is the part that used to break — the old engine survived
    /// the trip to the Menu and the new match never began.
    /// </summary>
    public class MatchFlowTests
    {
        private NetworkMode _savedMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = GameBootstrap.ConfigOrDefault;
            _savedMode = config.networkMode;
            config.networkMode = NetworkMode.Local;
            SceneManager.LoadScene(NetSession.MenuSceneName);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetSession.Instance != null)
            {
                var leave = NetSession.Instance.LeaveAsync();
                yield return WaitFor(() => leave.IsCompleted, 10f, "leaving the lobby");
            }
            GameBootstrap.ConfigOrDefault.networkMode = _savedMode;
        }

        [UnityTest]
        public IEnumerator HostCanPlayAMatchThenStartAnotherFromTheLobby()
        {
            var net = NetSession.Instance;
            Assert.IsNotNull(net, "the Bootstrap prefab should have created the NetSession");

            var host = net.HostAsync();
            yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
            Assert.IsTrue(host.Result, "hosting a Local lobby failed");

            net.StartGame();
            yield return WaitFor(EngineRunning, 15f, "the first match's engine");
            var first = GameState.Instance;
            Assert.AreEqual(1, first.RoundIndex.Value, "a new match starts at round one");
            Assert.AreEqual(GamePhase.RoundIntro, first.Phase.Value);
            if (!GameBootstrap.ConfigOrDefault.questionRounds)
                Assert.IsTrue(first.CurrentDef != null && first.CurrentDef.IsMinigame,
                    "question rounds are off, so round one has to be a minigame");

            net.ReturnToLobby();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == NetSession.MenuSceneName
                                       && GameState.Instance == null, 15f, "the lobby, with the engine gone");

            net.StartGame();
            yield return WaitFor(EngineRunning, 15f, "the second match's engine");
            Assert.AreNotSame(first, GameState.Instance, "the second match needs a fresh engine");
            Assert.AreEqual(1, GameState.Instance.RoundIndex.Value, "the second match starts at round one too");
            Assert.AreEqual(GamePhase.RoundIntro, GameState.Instance.Phase.Value);
        }

        /// <summary>
        /// The host's options reach the match: a short game plays to 50, and only the games left
        /// on are dealt. The options are swapped in for the test and never saved.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHostsOptionsShapeTheMatch()
        {
            var keep = new HashSet<string> { "simon", "maze", "stroop" };
            var options = new HostOptions();
            options.SetLength(0);
            foreach (var e in MinigameRegistry.All)
                if (!keep.Contains(e.Id)) options.SetOn(e.Id, false, MinigameRegistry.All);
            var saved = HostOptions.Replace(options);
            try
            {
                var net = NetSession.Instance;
                var host = net.HostAsync();
                yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
                Assert.IsTrue(host.Result, "hosting a Local lobby failed");
                yield return WaitFor(() => MatchSettings.Instance != null && MatchSettings.Instance.IsSpawned, 5f,
                    "the lobby's settings");
                Assert.AreEqual(50, MatchSettings.Target, "a short match plays to 50");
                Assert.AreEqual(3, MatchSettings.Instance.GamesOn.Value, "the lobby shows how many games are on");

                net.StartGame();
                yield return WaitFor(EngineRunning, 15f, "the match");
                Assert.AreEqual(50, MatchSettings.Target, "the settings ride along into the Game scene");
                var def = GameState.Instance.CurrentDef;
                if (def != null && def.IsMinigame)
                    Assert.IsTrue(keep.Contains(def.minigameId), $"round one ({def.minigameId}) should be one of the host's three");
            }
            finally
            {
                HostOptions.Replace(saved);
            }
        }

        /// <summary>The lobby gives the host a kick on everyone's seat but their own.</summary>
        [UnityTest]
        public IEnumerator TheHostHasNoKickOnTheirOwnSeat()
        {
            var net = NetSession.Instance;
            var host = net.HostAsync();
            yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
            Assert.IsTrue(host.Result, "hosting a Local lobby failed");
            var lobby = Object.FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
            lobby.ShowInstant();
            yield return null;
            var seats = Field<LobbySeat[]>(lobby, "seats");
            var kick = Field<Button>(seats[0], "kickButton");
            Assert.IsFalse(kick.gameObject.activeSelf, "no kicking yourself");
            net.Kick(NetworkManager.ServerClientId);
            yield return null;
            Assert.IsTrue(net.IsInSession, "and asking to kick the host does nothing");
        }

        /// <summary>The minigame list changes the deck the lobby shows, and keeps a few games on.</summary>
        [UnityTest]
        public IEnumerator ThePickerChangesTheDeck()
        {
            var saved = HostOptions.Replace(new HostOptions());
            try
            {
                var net = NetSession.Instance;
                var host = net.HostAsync();
                yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
                Assert.IsTrue(host.Result, "hosting a Local lobby failed");
                var lobby = Object.FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                lobby.ShowInstant();
                Field<Button>(lobby, "chooseButton").onClick.Invoke();
                var picker = Field<MinigamePicker>(lobby, "picker");
                yield return WaitFor(() => picker.IsShown, 3f, "the minigame list");

                // Every ticket but three, clicked: the last three refuse.
                var grid = Field<RectTransform>(picker, "grid");
                Assert.AreEqual(MinigameRegistry.All.Count, grid.childCount, "a ticket per game");
                foreach (Transform ticket in grid) ticket.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.AreEqual(HostOptions.MinGamesOn, HostOptions.Current.CountOn(MinigameRegistry.All));
                Assert.AreEqual(HostOptions.MinGamesOn, MatchSettings.Instance.GamesOn.Value, "the lobby sees it at once");
                Field<Button>(picker, "allOnButton").onClick.Invoke();
                yield return null;
                Assert.AreEqual(MinigameRegistry.All.Count, MatchSettings.Instance.GamesOn.Value);
                picker.HideInstant(); // not Close(): that would save the test's options on this machine
            }
            finally
            {
                HostOptions.Replace(saved);
            }
        }

        /// <summary>
        /// The match menu: open, it holds the game's input; END MATCH asks twice, then everyone
        /// (here, just the host) is back in the lobby, which says why.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHostCanEndTheMatchFromTheMenu()
        {
            var net = NetSession.Instance;
            var host = net.HostAsync();
            yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
            Assert.IsTrue(host.Result, "hosting a Local lobby failed");
            net.StartGame();
            yield return WaitFor(EngineRunning, 15f, "the match");

            var menu = Object.FindAnyObjectByType<MatchMenu>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the Game scene should have a MatchMenu");
            menu.Open();
            yield return WaitFor(() => menu.IsShown, 3f, "the match menu");
            Assert.IsTrue(KeyInput.Held, "an open menu holds the game's keys and mouse");

            var end = Field<Button>(menu, "endButton");
            Assert.IsTrue(end.gameObject.activeSelf, "the host gets END MATCH");
            end.onClick.Invoke();
            yield return null;
            Assert.AreEqual(NetSession.GameSceneName, SceneManager.GetActiveScene().name, "the first click only asks");
            end.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == NetSession.MenuSceneName
                                       && GameState.Instance == null, 15f, "the lobby");
            yield return null;
            Assert.IsFalse(KeyInput.Held, "the menu let go of the input");
            Assert.IsTrue(net.IsInSession, "ending the match keeps the lobby");
            var lobby = Object.FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
            Assert.AreEqual("The host ended the match.", Field<TMPro.TMP_Text>(lobby, "hintText").text);
        }

        /// <summary>The host's LEAVE closes the lobby and lands on the front page.</summary>
        [UnityTest]
        public IEnumerator TheHostCanLeaveFromTheMenu()
        {
            var net = NetSession.Instance;
            var host = net.HostAsync();
            yield return WaitFor(() => host.IsCompleted, 10f, "hosting");
            Assert.IsTrue(host.Result, "hosting a Local lobby failed");
            net.StartGame();
            yield return WaitFor(EngineRunning, 15f, "the match");

            var menu = Object.FindAnyObjectByType<MatchMenu>(FindObjectsInactive.Include);
            menu.Open();
            yield return WaitFor(() => menu.IsShown, 3f, "the match menu");
            var leave = Field<Button>(menu, "leaveButton");
            leave.onClick.Invoke();
            leave.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == NetSession.MenuSceneName && !net.IsInSession,
                15f, "the front page");
            yield return null;
            Assert.IsFalse(KeyInput.Held);
            Assert.IsNull(MatchSettings.Instance, "closing the lobby takes its settings with it");
        }

        private static T Field<T>(object target, string name) where T : class
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name} has no field {name}");
            return f.GetValue(target) as T;
        }

        private static bool EngineRunning()
            => GameState.Instance != null && GameState.Instance.IsSpawned && GameState.Instance.Phase.Value != GamePhase.Idle;

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float waited = 0f;
            while (!condition())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > seconds) Assert.Fail("Timed out waiting for " + what + ".");
                yield return null;
            }
        }
    }
}
