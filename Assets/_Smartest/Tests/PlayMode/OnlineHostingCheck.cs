using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Smartest.Tests
{
    /// <summary>
    /// Hosts a real online lobby the way the Host button does in Online mode: Unity Services
    /// sign-in, a private Relay session, a join code. It proves the project is linked and Relay
    /// and the session service are switched on for it, which no other test touches (they all
    /// play over 127.0.0.1). The session is left straight away.
    ///
    /// Explicit: it talks to Unity Gaming Services. Run it before a release:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter OnlineHostingCheck
    /// </summary>
    [Explicit("Creates a real Relay session through Unity Gaming Services; run it on purpose.")]
    public class OnlineHostingCheck
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator HostingOnlineGivesAJoinCode()
        {
            var config = GameBootstrap.ConfigOrDefault;
            var saved = config.networkMode;
            config.networkMode = NetworkMode.Online;
            SceneManager.LoadScene(NetSession.MenuSceneName);
            yield return null;
            yield return null;

            var session = NetSession.Instance;
            Assert.IsNotNull(session, "the Bootstrap prefab should have created the NetSession");
            var said = new List<string>();
            void Heard(string line) => said.Add(line);
            session.Status += Heard;

            var host = session.HostAsync();
            float t = 0f;
            while (!host.IsCompleted && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
            bool finished = host.IsCompleted, hosted = finished && host.Result;
            var mode = session.CurrentMode;
            string code = session.JoinCode;
            session.Status -= Heard;
            config.networkMode = saved;

            // Leave before judging, so a failed check never leaves a lobby behind.
            var leave = session.LeaveAsync();
            float waited = 0f;
            while (!leave.IsCompleted && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }

            string trail = string.Join(" / ", said);
            Assert.IsTrue(finished, "hosting online never finished: " + trail);
            Assert.IsTrue(hosted, "hosting online failed: " + trail);
            Assert.AreEqual(NetSession.Mode.Relay, mode, "hosted, but not over Relay: " + trail);
            Assert.IsFalse(string.IsNullOrEmpty(code), "a Relay lobby should have a join code");
            Debug.Log($"[OnlineHostingCheck] Relay lobby was up with join code {code}.");
        }
    }
}
