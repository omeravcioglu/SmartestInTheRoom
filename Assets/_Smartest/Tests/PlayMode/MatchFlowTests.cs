using System;
using System.Collections;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

            net.ReturnToLobby();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == NetSession.MenuSceneName
                                       && GameState.Instance == null, 15f, "the lobby, with the engine gone");

            net.StartGame();
            yield return WaitFor(EngineRunning, 15f, "the second match's engine");
            Assert.AreNotSame(first, GameState.Instance, "the second match needs a fresh engine");
            Assert.AreEqual(1, GameState.Instance.RoundIndex.Value, "the second match starts at round one too");
            Assert.AreEqual(GamePhase.RoundIntro, GameState.Instance.Phase.Value);
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
