using System.Diagnostics;

namespace AllynTech.MeadowTools.TaskScheduler
{
    internal static class CoreExtensions
    {
        /// <summary>
        /// Gets the elapsed time in milliseconds and then resets the timer
        /// </summary>
        /// <param name="timer"></param>
        /// <returns></returns>
        public static long GetElapsedMsAndReset(this Stopwatch timer)
        {
            var elapsed = timer.ElapsedMilliseconds;
            timer.Restart();
            return elapsed;
        }

        /// <summary>
        /// Gets the elapsed time in seconds
        /// </summary>
        /// <param name="timer"></param>
        /// <returns></returns>
        public static long GetElapsedSeconds(this Stopwatch timer)
        {
            return timer.ElapsedMilliseconds / 1000;
        }
    }
}
