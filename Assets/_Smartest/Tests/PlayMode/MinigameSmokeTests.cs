using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>
        /// The rule card plays each game's demo level (level 1, or the game's DemoLevel) by
        /// itself, before anyone plays it, on a loop: a fresh level for every run. On each of the
        /// first three runs, every demo has to run without throwing, show a move within its first
        /// seven seconds (the card is up for eight) — a hand on screen, a click or a key — and
        /// never lose. A demo that shows nothing teaches nothing; one that loses teaches it wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryMinigameDemoShowsAMoveAndWins()
        {
            var root = new GameObject("DemoCanvas", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var area = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root.transform, false);
            area.sizeDelta = new Vector2(1200f, 520f);
            var silent = new List<string>();
            var losing = new List<string>();

            try
            {
                foreach (var entry in MinigameRegistry.All)
                {
                    for (int run = 0; run < 3; run++)
                    {
                        string what = $"{entry.Id} (run {run})";
                        var go = new GameObject(entry.Id + "-demo", typeof(RectTransform));
                        go.transform.SetParent(root.transform, false);
                        try
                        {
                            var view = (MinigameView)go.AddComponent(entry.ViewType);
                            view.Demo = true;
                            bool acted = false, lost = false;
                            view.DemoTapped += _ => acted = true;
                            view.DemoKeyPressed += _ => acted = true;
                            view.Finished += (failed, _) => lost = failed;
                            view.Init(area);
                            int level = view.DemoLevel;
                            // The seeds the stage gives its runs.
                            view.Prepare(level, LevelRng.For(entry.Id, 0x5EED + run, level), entry.LevelSeconds, false);
                            view.Begin();
                            float firstMove = -1f;
                            // As long as the stage lets a run go before it starts over.
                            for (float t = 0f; t < 12f && !view.IsDone; t += Step)
                            {
                                view.Advance(Step);
                                if (firstMove < 0f && (acted || view.DemoHand.Shown)) firstMove = t;
                            }
                            if (firstMove < 0f || firstMove > 7f) silent.Add(what);
                            if (lost) losing.Add(what);
                            view.Teardown();
                        }
                        catch (Exception e) when (!(e is AssertionException))
                        {
                            Assert.Fail(what + "'s demo threw: " + e);
                        }
                        finally
                        {
                            Object.Destroy(go);
                        }
                    }
                    yield return null;
                }
                Assert.IsEmpty(silent, "these demos show no move in time: " + string.Join(", ", silent));
                Assert.IsEmpty(losing, "these demos lose: " + string.Join(", ", losing));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
