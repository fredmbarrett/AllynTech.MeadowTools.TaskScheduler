// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using AllynTech.MeadowTools.TaskScheduler.DataModels;
using System;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler.Factories
{
    /// <summary>
    /// Factory responsible for converting <see cref="SensorPollSchedule"/> definitions
    /// into executable <see cref="ScheduleEntry"/> instances that can be registered with
    /// the <see cref="SchedulerService"/>.
    /// 
    /// This factory interprets encoded time fields (<c>ActionHour</c>, <c>ActionMinute</c>,
    /// <c>ActionSecond</c>) to determine whether the schedule is:
    /// 
    /// • **Per-second interval** → ActionSecond > 59 → "Every N seconds" (N = ActionSecond - 60).  
    /// • **Per-minute interval** → ActionHour = 25 and ActionMinute ≥ 60 → "Every N minutes".  
    /// • **Hourly aligned** → ActionHour ≥ 24 → "Every (ActionHour - 24) hours at ActionMinute past".  
    /// • **Daily aligned** → ActionHour &lt; 24 → "Once per day at HH:MM".  
    /// 
    /// Sensor Poll actions are constrained to a maximum execution time of
    /// <see cref="MAX_EXECUTION_SECONDS"/> (15s) to prevent long-running sensor
    /// operations from blocking the scheduler.
    /// </summary>
    public class SensorPollScheduleFactory : ScheduleFactoryBase
    {
        /// <summary>
        /// Maximum expected execution time for a sensor poll job.
        /// Used as a soft upper bound for diagnostics/watchdogs.
        /// </summary>
        private const int MAX_EXECUTION_SECONDS = 15;

        /// <summary>
        /// Creates a <see cref="ScheduleEntry"/> from a <see cref="SensorPollSchedule"/>,
        /// decoding its encoded fields into either an interval-based or aligned schedule.
        /// </summary>
        /// <param name="schedule">The <see cref="SensorPollSchedule"/> definition.</param>
        /// <param name="work">The delegate to invoke when the schedule fires.</param>
        /// <returns>A ready-to-register <see cref="ScheduleEntry"/>.</returns>
        public static ScheduleEntry From(
            SensorPollSchedule schedule,
            Func<SensorPollSchedule, Task> work)
        {
            // Interval: "Every N seconds".
            if (schedule.ActionSecond > 59)
            {
                var interval = TimeSpan.FromSeconds(schedule.ActionSecond - 60);
                return BuildIntervalSchedule(schedule, interval, work);
            }

            // Interval: "Every N minutes".
            if (schedule.ActionHour == 25 && schedule.ActionMinute >= 60)
            {
                var interval = TimeSpan.FromMinutes(schedule.ActionMinute - 60);
                return BuildIntervalSchedule(schedule, interval, work);
            }

            // Hourly aligned schedule: e.g., "Every 6 hours at 15 minutes past".
            if (schedule.ActionHour >= 24)
            {
                int intervalHours = schedule.ActionHour - 24;
                int minuteOffset = schedule.ActionMinute;
                return FromAligned(schedule, intervalHours, minuteOffset, 0, work);
            }

            // Daily aligned schedule: e.g., "Once per day at HH:MM".
            else
            {
                int hourOfDay = schedule.ActionHour;
                int minuteOffset = schedule.ActionMinute;
                int minutesCalculated = minuteOffset + hourOfDay * 60;
                return FromAligned(schedule, 24, minutesCalculated, 0, work);
            }
        }

        /// <summary>
        /// Creates an interval-based schedule entry for second/minute cadences.
        /// Examples: "Every 10 seconds", "Every 15 minutes".
        /// </summary>
        private static ScheduleEntry BuildIntervalSchedule(
            SensorPollSchedule schedule,
            TimeSpan baseInterval,
            Func<SensorPollSchedule, Task> work)
        {
            Log.Debug($"[SensorPoll] base interval {baseInterval}");

            // Compute next run by incrementing the base interval.
            // If the device missed one or more triggers (e.g., sleep),
            // advance until the next valid time in the future.
            DateTime ComputeNext(DateTime lastPlanned, TimeSpan _)
            {
                var next = lastPlanned + baseInterval;
                while (next <= DateTime.UtcNow)
                    next += baseInterval;
                return next;
            }

            // sample derived sensor name
            var sensorName = schedule.ActionParam switch
            {
                1 => "Particle Sensor",
                2 => "VOC Sensor",
                _ => "Unknown Sensor"
            };

            return new ScheduleEntry(
                schedule.Id,
                sensorName,
                ScheduleKind.SensorPoll,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                DateTime.UtcNow, // SchedulerService recomputes on AddOrReplace
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }

        /// <summary>
        /// Creates a time-aligned schedule entry (hourly or daily).
        /// Examples: "Every 4 hours at 10 minutes past", "Daily at 23:15".
        /// </summary>
        /// <param name="intervalHours">Interval in hours (1–24).</param>
        /// <param name="minuteOffset">Minute within the interval to run.</param>
        /// <param name="secondOffset">Second offset (usually 0).</param>
        private static ScheduleEntry FromAligned(
            SensorPollSchedule schedule,
            int intervalHours,
            int minuteOffset,
            int secondOffset,
            Func<SensorPollSchedule, Task> work)
        {
            if (intervalHours <= 0 || intervalHours > 24)
                throw new ArgumentOutOfRangeException(nameof(intervalHours));

            TimeSpan baseInterval = TimeSpan.FromHours(intervalHours);
            Log.Debug($"[SensorPoll] aligned interval {baseInterval} offset {minuteOffset}:{secondOffset:D2}");

            // Helper: aligns to the next valid boundary (minute/second offsets,
            // hour divisible by intervalHours, and strictly in the future).
            DateTime Align(DateTime t)
            {
                var aligned = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc)
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

            // sample derived sensor name
            var sensorName = schedule.ActionParam switch
            {
                1 => "Particle Sensor",
                2 => "VOC Sensor",
                _ => "Unknown Sensor"
            };

            return new ScheduleEntry(
                schedule.Id,
                sensorName,
                ScheduleKind.SensorPoll,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                firstRun,
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }
    }

}
