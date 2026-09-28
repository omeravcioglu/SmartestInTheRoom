using System;
using System.Collections;
using NUnit.Framework;
using Smartest.Minigames;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// Every level from 1 to 12 of every minigame, played headless with a fixed time step:
    /// once as a player who never touches anything, once as someone watching. Nothing here
    /// judges whether a level is fun or fair, only that none of them throws while it is built
    /// or played, and that each one ends. It runs in seconds, not the minutes the levels take.
    /// </summary>
    public class MinigameSmokeTests
    {
        private const float Step = 1f / 30f;
        private const int Levels = 12;

        [UnityTest]
        public IEnumerator EveryLevelOfEveryMinigameRunsToAnEnd()
        {
            var root = new GameObject("SmokeCanvas", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var events = new GameObject("EventSystem", typeof(EventSystem));
            var area = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root.transform, false);
            area.sizeDelta = new Vector2(1200f, 520f);
            int runs = 0;

            try
            {
                foreach (var entry in MinigameRegistry.All)
                {
                    for (int level = 1; level <= Levels; level++)
                    {
                        foreach (bool playing in new[] { true, false })
                        {
                            string what = $"{entry.Id} level {level} ({(playing ? "playing" : "watching")})";
                            var go = new GameObject(entry.Id, typeof(RectTransform));
                            go.transform.SetParent(root.transform, false);
                            try
                            {
                                var view = (MinigameView)go.AddComponent(entry.ViewType);
                                view.Init(area);
                                view.Prepare(level, LevelRng.For(entry.Id, 20260928, level), entry.LevelSeconds, playing);
                                view.Begin();
                                for (float t = 0f; !view.IsDone && t < entry.LevelSeconds; t += Step) view.Advance(Step);
                                if (!view.IsDone) view.TimeOut(); // what the stage does at the deadline
                                Assert.IsTrue(view.IsDone, what + " never ended");
                                view.Teardown();
                                runs++;
                            }
                            catch (Exception e) when (!(e is AssertionException))
                            {
                                Assert.Fail(what + " threw: " + e);
                            }
                            finally
                            {
                                Object.Destroy(go);
                            }
                        }
                    }
                    yield return null; // let this game's pieces be destroyed before the next one
                }
                Assert.AreEqual(MinigameRegistry.All.Count * Levels * 2, runs, "every level of every game, both ways");
            }
            finally
            {
                Object.Destroy(events);
                Object.Destroy(root);
            }
        }
    }
}
