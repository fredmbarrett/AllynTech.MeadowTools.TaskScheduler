// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using AllynTech.MeadowTools.TaskScheduler.DataModels;
using System;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler.Factories
{
    /// <summary>
    /// Factory responsible for converting <see cref="CallInSchedule"/> database/config objects
    /// into executable <see cref="ScheduleEntry"/> instances that can be registered with the 
    /// <see cref="SchedulerService"/>.
    /// 
    /// This class interprets the <c>ActionHour</c> and <c>ActionMinute</c> encodings used by 
    /// the server/device protocol to represent three types of call-in schedules:
    /// 
    /// • **Per-minute interval**: ActionHour = 25, ActionMinute ≥ 60
    ///     → run every (ActionMinute - 60) minutes.
    /// 
    /// • **Hourly interval, aligned to minutes**: ActionHour ≥ 24
    ///     → run every (ActionHour - 24) hours, aligned to ActionMinute within each hour.
    /// 
    /// • **Daily at a fixed time**: ActionHour < 24
    ///     → run once per day at the given hour/minute combination.
    /// 
    /// All jobs are wrapped in a <see cref="ScheduleEntry"/> with a hard runtime cap
    /// of <see cref="MAX_EXECUTION_SECONDS"/> to guard against runaway tasks.
    /// </summary>
    public class CallInScheduleEntryFactory : ScheduleFactoryBase
    {
        /// <summary>
        /// Maximum expected execution time for a call-in action.
        /// Used as a soft upper bound for diagnostics/watchdogs.
        /// </summary>
        private const int MAX_EXECUTION_SECONDS = 90;
        private const string SCHEDULE_NAME = "Server Call-In";

        /// <summary>
        /// Creates a <see cref="ScheduleEntry"/> from a <see cref="CallInSchedule"/>,
        /// decoding its encoded <c>ActionHour</c>/<c>ActionMinute</c> fields into
        /// either an interval schedule or an aligned daily/hourly schedule.
        /// </summary>
        /// <param name="schedule">The <see cref="CallInSchedule"/> definition.</param>
        /// <param name="work">The delegate to invoke when the schedule fires.</param>
        /// <returns>A ready-to-register <see cref="ScheduleEntry"/>.</returns>
        public static ScheduleEntry From(
            CallInSchedule schedule,
            Func<CallInSchedule, Task> work)
        {
            // Interval schedule: "Every N minutes".
            if (schedule.ActionHour == 25 && schedule.ActionMinute >= 60)
            {
                var interval = TimeSpan.FromMinutes(schedule.ActionMinute - 60);
                return BuildIntervalSchedule(schedule, interval, work);
            }

            // Hourly schedule: "Every N hours at minute offset".
            if (schedule.ActionHour >= 24)
            {
                int intervalHours = schedule.ActionHour - 24;
                int minuteOffset = schedule.ActionMinute;
                return FromAligned(schedule, intervalHours, minuteOffset, 0, work);
            }

            // Daily schedule: "Once per day at HH:MM".
            else
            {
                int hourOfDay = schedule.ActionHour;
                int minuteOffset = schedule.ActionMinute;
                int minutesCalculated = minuteOffset + hourOfDay * 60;
                return FromAligned(schedule, 24, minutesCalculated, 0, work);
            }
        }

        /// <summary>
        /// Builds a simple interval-based <see cref="ScheduleEntry"/>.
        /// Example: "Execute every 15 minutes".
        /// </summary>
        private static ScheduleEntry BuildIntervalSchedule(
            CallInSchedule schedule,
            TimeSpan baseInterval,
            Func<CallInSchedule, Task> work)
        {
            Log.Debug($"[CallIn] base interval {baseInterval}");

            // Computes the next run time strictly by incrementing the base interval.
            // If multiple firings were missed (e.g. device slept), it advances until 
            // the next future-aligned time.
            DateTime ComputeNext(DateTime lastPlanned, TimeSpan _)
            {
                var next = lastPlanned + baseInterval;
                while (next <= DateTime.UtcNow)
                    next += baseInterval; // catch up
                return next;
            }

            return new ScheduleEntry(
                schedule.Id,
                SCHEDULE_NAME,
                ScheduleKind.CallIn,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                DateTime.UtcNow, // Scheduler will recalc on registration
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }

        /// <summary>
        /// Builds a time-aligned <see cref="ScheduleEntry"/>.  
        /// Example: "Every 4 hours at 10 minutes past the hour", or "Daily at 23:15".
        /// </summary>
        /// <param name="intervalHours">Interval in hours (1–24).</param>
        /// <param name="minuteOffset">Minute within the interval to run at.</param>
        /// <param name="secondOffset">Second offset (usually zero).</param>
        private static ScheduleEntry FromAligned(
            CallInSchedule schedule,
            int intervalHours,
            int minuteOffset,
            int secondOffset,
            Func<CallInSchedule, Task> work)
        {
            if (intervalHours <= 0 || intervalHours > 24)
                throw new ArgumentOutOfRangeException(nameof(intervalHours));

            TimeSpan baseInterval = TimeSpan.FromHours(intervalHours);
            Log.Debug($"[CallIn] aligned interval {baseInterval} offset {minuteOffset}:{secondOffset:D2}");

            // Aligns a time to the next valid boundary: correct minute/second offset,
            // hour divisible by intervalHours, and strictly in the future.
            DateTime Align(DateTime t)
            {
                var aligned = new DateTime(
                    t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc)
                    .AddMinutes(minuteOffset)
                    .AddSeconds(secondOffset);

                while (aligned <= t || (aligned.Hour % intervalHours) != 0)
                    aligned = aligned.AddHours(1);

                return aligned;
            }

            DateTime firstRun = Align(DateTime.UtcNow);

            DateTime ComputeNext(DateTime lastPlanned, TimeSpan _)
            {
                var tentative = lastPlanned + baseInterval;
                return Align(tentative);
            }

            return new ScheduleEntry(
                schedule.Id,
                SCHEDULE_NAME,
                ScheduleKind.CallIn,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                firstRun,
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }
    }

}
