using System;

namespace AllynTech.MeadowTools.TaskScheduler.DataModels
{
    public abstract class ScheduleBase
    {
        /// <summary>
        /// Unique identifier for the schedule record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Version number of this schedule set, used for sync/version tracking.
        /// </summary>
        public int ScheduleVersion { get; set; }

        /// <summary>
        /// Type/category of schedule (reserved for server/device protocol).
        /// </summary>
        public int ScheduleType { get; set; }

        /// <summary>
        /// 7-bit mask representing days of week schedule is active (bit0=Sun … bit6=Sat).
        /// </summary>
        public int ActionDays { get; set; }

        /// <summary>
        /// Encoded action type (0x01=Check-In, 0x02=Report, etc.).
        /// </summary>
        public virtual int ActionType { get; set; }

        /// <summary>
        /// Encoded hour component of schedule.  
        /// May contain reserved values (≥24) to indicate interval mode.
        /// </summary>
        public int ActionHour { get; set; }

        /// <summary>
        /// Encoded minute component of schedule.  
        /// May contain reserved values (≥60) to indicate minute interval mode.
        /// </summary>
        public int ActionMinute { get; set; }

        /// <summary>
        /// Indicates whether this schedule is active and should be considered
        /// by the scheduler service.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Returns true if this schedule is valid for the current day of week,
        /// based on <see cref="ActionDays"/>.
        /// </summary>
        public bool IsValidToday
        {
            get
            {
                var today = 0x01 << (int)DateTime.Now.DayOfWeek;
                return (ActionDays & today) != 0;
            }
        }
    }
}
