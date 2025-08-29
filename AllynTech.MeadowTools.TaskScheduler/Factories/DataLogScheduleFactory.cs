// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using AllynTech.MeadowTools.TaskScheduler.DataModels;
using System;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler.Factories
{
    /// <summary>
    /// Factory responsible for converting <see cref="DataLogSchedule"/> database/config
    /// objects into executable <see cref="ScheduleEntry"/> instances that can be registered
    /// with the <see cref="SchedulerService"/>.
    /// 
    /// Interprets the encoded <c>ActionHour</c>, <c>ActionMinute</c>, and <c>ActionSecond</c>
    /// fields from the schedule into one of the following time semantics:
    /// 
    /// • **Per-second interval** → ActionSecond > 59 → "Every N seconds" (N = ActionSecond - 60).  
    /// • **Per-minute interval** → ActionHour = 25, ActionMinute ≥ 60 → "Every N minutes".  
    /// • **Hourly aligned** → ActionHour ≥ 24 → "Every (ActionHour - 24) hours at minute offset".  
    /// • **Daily aligned** → ActionHour < 24 → "Occurs once per day at HH:MM".  
    /// 
    /// Jobs are wrapped in a <see cref="ScheduleEntry"/> with a hard runtime cap
    /// of <see cref="MAX_EXECUTION_SECONDS"/> (30s), ensuring data logging never blocks
    /// the scheduler for an excessive amount of time.
    /// </summary>
    public class DataLogScheduleFactory : ScheduleFactoryBase
    {
        /// <summary>
        /// Maximum expected execution time for a data log action.
        /// Used as a soft upper bound for diagnostics/watchdogs.
        /// </summary>
        private const int MAX_EXECUTION_SECONDS = 30;

        /// <summary>
        /// Creates a <see cref="ScheduleEntry"/> from a <see cref="DataLogSchedule"/>,
        /// decoding its encoded fields into either an interval-based or aligned schedule.
        /// </summary>
        /// <param name="schedule">The <see cref="DataLogSchedule"/> definition.</param>
        /// <param name="work">The delegate to invoke when the schedule fires.</param>
        /// <returns>A ready-to-register <see cref="ScheduleEntry"/>.</returns>
        public static ScheduleEntry From(
            DataLogSchedule schedule,
            Func<DataLogSchedule, Task> work)
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
        /// Creates an interval-based schedule entry from a DataLogSchedule.
        /// Examples: "Every 10 seconds", "Every 15 minutes".
        /// </summary>
        private static ScheduleEntry BuildIntervalSchedule(
            DataLogSchedule schedule,
            TimeSpan baseInterval,
            Func<DataLogSchedule, Task> work)
        {
            Log.Debug($"[DataLog] base interval {baseInterval}");

            // Compute next run by incrementing the base interval.
            // If the device missed one or more triggers (e.g., sleep),
            // advance until the next future-aligned time.
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
                ScheduleKind.DataLog,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                DateTime.UtcNow, // SchedulerService recomputes on AddOrReplace
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }

        /// <summary>
        /// Creates a time-aligned schedule entry from a DataLogSchedule.
        /// Examples: "Every 4 hours at 10 minutes past" or "Daily at 23:15".
        /// </summary>
        /// <param name="intervalHours">Interval in hours (1–24).</param>
        /// <param name="minuteOffset">Minute within the interval to run.</param>
        /// <param name="secondOffset">Second offset (usually 0).</param>
        private static ScheduleEntry FromAligned(
            DataLogSchedule schedule,
            int intervalHours,
            int minuteOffset,
            int secondOffset,
            Func<DataLogSchedule, Task> work)
        {
            if (intervalHours <= 0 || intervalHours > 24)
                throw new ArgumentOutOfRangeException(nameof(intervalHours));

            TimeSpan baseInterval = TimeSpan.FromHours(intervalHours);
            Log.Debug($"[DataLog] aligned interval {baseInterval} offset {minuteOffset}:{secondOffset:D2}");

            // Aligns to the next boundary: correct minute/second offsets,
            // hour divisible by intervalHours, strictly in the future.
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
                ScheduleKind.DataLog,
                TimeSpan.FromSeconds(MAX_EXECUTION_SECONDS),
                firstRun,
                async (_) => await work(schedule).ConfigureAwait(false),
                ComputeNext);
        }
    }
}
