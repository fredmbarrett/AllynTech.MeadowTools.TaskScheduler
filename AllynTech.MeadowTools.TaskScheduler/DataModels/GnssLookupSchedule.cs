using System.Text;

namespace AllynTech.MeadowTools.TaskScheduler.DataModels
{
    /// <summary>
    /// Represents a GNSS (GPS) lookup schedule configuration, typically created by the
    /// server or persisted in the device database. Defines the cadence at which
    /// GNSS lookups should occur, including days of week, encoded hour/minute fields,
    /// and the action type.
    /// 
    /// Encoding rules for <see cref="ActionHour"/> and <see cref="ActionMinute"/>:
    /// • ActionHour = 25 and ActionMinute ≥ 60 → "Every N minutes" (N = ActionMinute - 60).  
    /// • ActionHour ≥ 24 → "Every (ActionHour - 24) hours at ActionMinute past the hour".  
    /// • Otherwise → "Occurs daily at HH:MM".
    /// 
    /// Defaults:
    /// • <see cref="ActionDays"/> = 0x7F → schedule is active all week (Sunday–Saturday).  
    /// • <see cref="ActionType"/> = 0x01 → GNSS/GPS lookup.  
    /// </summary>
    public class GnssLookupSchedule : ScheduleBase
    {
        /// <summary>
        /// Default to ActionType = 0x01 (GNSS Lookup).
        /// </summary>
        public override int ActionType { get; set; } = 0x01;

        public GnssLookupSchedule() { }

        public override string ToString()
        {
            string days = ((ActionDays & 0x01) == 0x01) ? "S" : "-";
            days += ((ActionDays & 0x02) == 0x02) ? "M" : "-";
            days += ((ActionDays & 0x04) == 0x04) ? "T" : "-";
            days += ((ActionDays & 0x08) == 0x08) ? "W" : "-";
            days += ((ActionDays & 0x10) == 0x10) ? "T" : "-";
            days += ((ActionDays & 0x20) == 0x20) ? "F" : "-";
            days += ((ActionDays & 0x40) == 0x40) ? "S" : "-";

            string time;
            if (ActionHour == 25 && ActionMinute >= 60) { time = $"Every {ActionMinute} minutes"; }
            else if (ActionHour >= 24 && ActionMinute <= 60) { time = $"Every {ActionHour} at {ActionMinute} past the hour"; }
            else time = $"Occurs at {ActionHour:D2}:{ActionMinute:D2}";

            string action = ActionType switch
            {
                0x01 => "GPS Lookup",
                _ => "Error",
            };

            var sb = new StringBuilder()
                .Append($"Id: {Id}; ")
                .Append($"Version: {ScheduleVersion}; ")
                .Append($"Days: {days}; ")
                .Append($"When: {time}; ")
                .Append($"Action: {action}");

            return sb.ToString();
        }
    }
}
