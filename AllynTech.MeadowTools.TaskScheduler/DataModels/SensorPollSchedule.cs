using System.Text;

namespace AllynTech.MeadowTools.TaskScheduler.DataModels
{
    /// <summary>
    /// Represents a schedule entry for periodic sensor polling.
    /// 
    /// A <see cref="SensorPollSchedule"/> defines:
    /// - The days of the week when the poll is active.
    /// - The exact or recurring time specification (hour, minute, second).
    /// - The action type (typically "Read Sensor Data").
    /// - An optional parameter (e.g., sensor ID or configuration value).
    /// 
    /// Encoding Rules for <see cref="ActionHour"/>, <see cref="ActionMinute"/>, <see cref="ActionSecond"/>:
    /// • ActionSecond > 59 → "Every N seconds" (N = ActionSecond - 60).  
    /// • ActionHour = 25 and ActionMinute ≥ 60 → "Every N minutes" (N = ActionMinute - 60).  
    /// • ActionHour ≥ 24 → "Every (ActionHour - 24) hours at ActionMinute past the hour".  
    /// • Otherwise → occurs daily at HH:MM:SS.  
    /// 
    /// Defaults:
    /// • <see cref="ActionDays"/> = 0x7F (all days Sunday–Saturday).  
    /// • <see cref="ActionType"/> = 0x01 (Read Sensor Data).  
    /// </summary>
    public class SensorPollSchedule : ScheduleBase
    {
        /// <summary>
        /// Default to ActionType = 0x01 (Sensor poll).
        /// </summary>
        public override int ActionType { get; set; } = 0x01;

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
        public SensorPollSchedule() { IsActive = true; }

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
            if (ActionSecond > 59) { time = $"Every {ActionSecond - 60} seconds"; }
            else if (ActionHour == 25 && ActionMinute >= 60) { time = $"Every {ActionMinute - 60} minutes"; }
            else if (ActionHour >= 24 && ActionMinute <= 60) { time = $"Every {ActionHour} at {ActionMinute} past the hour"; }
            else time = $"Occurs at {ActionHour:D2}:{ActionMinute:D2}:{ActionSecond:D2}";

            string action = ActionType switch
            {
                0x01 => "Read Sensor Data",
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
