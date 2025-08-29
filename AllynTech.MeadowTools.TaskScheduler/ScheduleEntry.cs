// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler
{
    /// <summary>
    /// Represents a single scheduled job managed by <see cref="SchedulerService"/>.
    /// Encapsulates identifying metadata, scheduling logic, and the delegate
    /// that performs the work when the entry is executed.
    /// </summary>
    public sealed class ScheduleEntry
    {
        /// <summary>
        /// Unique identifier for this schedule instance (per <see cref="ScheduleKind"/>).
        /// Used for lookups, replacement, and removal.
        /// </summary>
        public int ScheduleId { get; }

        /// <summary>
        /// Optional friendly name for logging and diagnostics.
        /// </summary>
        public string ScheduleName { get; set; } = string.Empty;

        /// <summary>
        /// Categorization of this schedule (e.g., CallIn, DataLog, Gnss).
        /// </summary>
        public ScheduleKind Kind { get; }

        /// <summary>
        /// The next UTC time this job is due to execute.
        /// Updated after each run using <see cref="ComputeNext"/>.
        /// </summary>
        public DateTime NextRunUtc { get; private set; }

        /// <summary>
        /// The asynchronous delegate containing the actual work to perform
        /// when this schedule fires.
        /// </summary>
        public Func<CancellationToken, Task> Work { get; }

        /// <summary>
        /// Delegate that computes the next execution time after a job run,
        /// given the last scheduled run and the observed runtime.
        /// </summary>
        public Func<DateTime, TimeSpan, DateTime> ComputeNext { get; }

        /// <summary>
        /// Upper bound on expected execution time for this job.
        /// Used for diagnostics, monitoring, or watchdog logic.
        /// Defaults to 60 seconds.
        /// </summary>
        public TimeSpan MaxAllowedRuntime { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Constructs a new <see cref="ScheduleEntry"/> with the provided parameters.
        /// </summary>
        /// <param name="scheduleId">Unique identifier for this schedule.</param>
        /// <param name="scheduleName">Friendly name for diagnostics.</param>
        /// <param name="kind">Type/category of schedule.</param>
        /// <param name="maxRuntime">Maximum allowed runtime for this job.</param>
        /// <param name="nextRunUtc">Initial UTC run time for this job.</param>
        /// <param name="work">Delegate containing the work to perform.</param>
        /// <param name="computeNext">
        /// Delegate that determines the next run time after a job completes.
        /// </param>
        public ScheduleEntry(
            int scheduleId,
            string scheduleName,
            ScheduleKind kind,
            TimeSpan maxRuntime,
            DateTime nextRunUtc,
            Func<CancellationToken, Task> work,
            Func<DateTime, TimeSpan, DateTime> computeNext
        )
        {
            ScheduleId = scheduleId;
            ScheduleName = scheduleName;
            Kind = kind;
            MaxAllowedRuntime = maxRuntime;
            NextRunUtc = nextRunUtc;
            Work = work;
            ComputeNext = computeNext;
        }

        /// <summary>
        /// Updates <see cref="NextRunUtc"/> after the job has been scheduled.
        /// </summary>
        /// <param name="next">The next UTC time the job should execute.</param>
        public void SetNext(DateTime next) => NextRunUtc = next;
    }


    /// <summary>
    /// Indicates which persistence table the schedule came from.
    /// Helps avoid collisions when two tables share a numeric PK.
    /// </summary>
    public enum ScheduleKind
    {
        CallIn,
        DataLog,
        SensorPoll,
        Gnss
    }
}
