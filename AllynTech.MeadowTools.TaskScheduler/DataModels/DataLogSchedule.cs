using System.Text;

namespace AllynTech.MeadowTools.TaskScheduler.DataModels
{
    /// <summary>
    /// Represents a data logging schedule stored in the device database.
    /// 
    /// A DataLogSchedule defines:
    /// - When the device should execute a data logging action
    /// - On which days of the week the schedule is active
    /// - What action should be performed (e.g., Record Data, Power On/Off)
    /// - Optional parameters associated with that action
    /// 
    /// Encoding Rules:
    /// • <see cref="ActionDays"/> is a 7-bit mask (SMTWTFS), where bit set = active day.
    /// • <see cref="ActionHour"/> and <see cref="ActionMinute"/> use reserved encodings:
    ///     – ActionSecond > 59 → "Every N seconds" (N = ActionSecond - 60).
    ///     – ActionHour = 25 and ActionMinute ≥ 60 → "Every N minutes" (N = ActionMinute - 60).
    ///     – ActionHour ≥ 24 → "Every (ActionHour - 24) hours at ActionMinute past the hour".
    ///     – Otherwise → occurs daily at HH:MM:SS.
    /// • <see cref="ActionType"/> maps to a fixed set of data logging actions.
    /// 
    /// Implements <see cref="IScheduleAction"/> so it can be converted into
    /// <see cref="ScheduleEntry"/> instances and managed by the <see cref="SchedulerService"/>.
    /// </summary>
    public class DataLogSchedule : ScheduleBase
    {
        /// <summary>
        /// Encoded seconds component (may contain reserved values for sub-minute intervals).
        /// </summary>
        public int ActionSecond { get; set; }

        /// <summary>
        /// Parameter passed to the action (e.g., sensor ID or configuration value).
        /// </summary>
        public int ActionParam { get; set; }

        /// <summary>
        /// Default constructor; initializes new schedules as active.
        /// </summary>
        public DataLogSchedule() { IsActive = true; }

        /// <summary>
        /// Returns a human-readable description of the schedule,
        /// including days, time semantics, action, and parameters.
        /// </summary>
        public override string ToString()
        {
            // Decode ActionDays into SMTWTFS string
            string days = ((ActionDays & 0x01) == 0x01) ? "S" : "-";
            days += ((ActionDays & 0x02) == 0x02) ? "M" : "-";
            days += ((ActionDays & 0x04) == 0x04) ? "T" : "-";
            days += ((ActionDays & 0x08) == 0x08) ? "W" : "-";
            days += ((ActionDays & 0x10) == 0x10) ? "T" : "-";
            days += ((ActionDays & 0x20) == 0x20) ? "F" : "-";
            days += ((ActionDays & 0x40) == 0x40) ? "S" : "-";

            // Decode ActionHour/ActionMinute/ActionSecond into a human-readable time
            string time;
            if (ActionSecond > 59)
            {
                time = $"Every {ActionSecond - 60} seconds";
            }
            else if (ActionHour == 25 && ActionMinute >= 60)
            {
                time = $"Every {ActionMinute - 60} minutes";
            }
            else if (ActionHour >= 24 && ActionMinute <= 60)
            {
                time = $"Every {ActionHour} at {ActionMinute} past the hour";
            }
            else
            {
                time = $"Occurs at {ActionHour:D2}:{ActionMinute:D2}:{ActionSecond:D2}";
            }

            // Decode ActionType into friendly string
            string action = ActionType switch
            {
                0x01 => "Power ON",
                0x02 => "Power OFF",
                0x04 => "Record Data",
                _ => "Error",
            };

            var sb = new StringBuilder()
                .Append($"Id: {Id}, ")
                .Append($"Version: {ScheduleVersion}, ")
                .Append($"Days: {days}, ")
                .Append($"When: {time}, ")
                .Append($"Action: {action}, ")
                .Append($"Param: {ActionParam}");

            return sb.ToString();
        }
    }
}
