using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// The clock everything on screen moves by: unscaled time, so a paused timeScale never freezes
    /// the UI. Normally it is exactly Time.unscaledTime / unscaledDeltaTime. A recorder can set a
    /// fixed step: then every frame moves the game on by exactly that much, however long the frame
    /// took to draw and save, which is how the trailer gets a true 60 fps with no hitches.
    /// (Time.captureDeltaTime would do this for scaled time only.)
    /// </summary>
    public static class GameClock
    {
        private static float s_step;
        private static float s_now;
        private static int s_frame;

        /// <summary>Seconds per frame while recording; 0 means real time.</summary>
        public static float FixedStep => s_step;

        public static void SetFixedStep(float seconds)
        {
            s_now = Now;
            s_frame = Time.frameCount;
            s_step = Mathf.Max(0f, seconds);
        }

        public static float Delta => s_step > 0f ? s_step : Time.unscaledDeltaTime;

        public static float Now
        {
            get
            {
                if (s_step <= 0f) return Time.unscaledTime;
                int frame = Time.frameCount;
                if (frame != s_frame)
                {
                    s_now += s_step * (frame - s_frame);
                    s_frame = frame;
                }
                return s_now;
            }
        }
    }
}
