using System.Text;

namespace AllynTech.MeadowTools.TaskScheduler.DataModels
{
    /// <summary>
    /// Represents a call-in schedule entry as defined by the server or device
    /// configuration. Encodes the days of week, time specification, and
    /// action to be performed when the schedule fires.
    /// 
    /// Interpretation rules:
    /// • <see cref="ActionDays"/> is a 7-bit mask (SMTWTFS), where bit set = active day.
    /// • <see cref="ActionHour"/> and <see cref="ActionMinute"/> are encoded values:
    ///     – ActionHour = 25 and ActionMinute ≥ 60 → "Every N minutes" (N = ActionMinute - 60).
    ///     – ActionHour ≥ 24 → "Every (ActionHour - 24) hours at ActionMinute past the hour".
    ///     – ActionHour &lt; 24 → "Occurs daily at HH:MM".
    /// • <see cref="ActionType"/> maps to specific device actions (e.g., Check-In, Report).
    /// 
    /// The <see cref="ToString"/> override renders a human-readable summary of the schedule.
    /// </summary>
    public partial class CallInSchedule : ScheduleBase
    {
        /// <summary>
        /// Default constructor (parameterless, required for EF/ORM and deserialization).
        /// </summary>
        public CallInSchedule() { }

        /// <summary>
        /// Returns a human-readable description of the schedule, including
        /// active days, time semantics, and action type.
        /// </summary>
        public override string ToString()
        {
            // Decode bitmask into SMTWTFS string
            string days = ((ActionDays & 0x01) == 0x01) ? "S" : "-";
            days += ((ActionDays & 0x02) == 0x02) ? "M" : "-";
            days += ((ActionDays & 0x04) == 0x04) ? "T" : "-";
            days += ((ActionDays & 0x08) == 0x08) ? "W" : "-";
            days += ((ActionDays & 0x10) == 0x10) ? "T" : "-";
            days += ((ActionDays & 0x20) == 0x20) ? "F" : "-";
            days += ((ActionDays & 0x40) == 0x40) ? "S" : "-";

            // Decode schedule time based on ActionHour/ActionMinute encoding rules
            string time;
            if (ActionHour == 25 && ActionMinute >= 60)
            {
                time = $"Every {ActionMinute - 60} minutes";
            }
            else if (ActionHour >= 24 && ActionMinute <= 60)
            {
                time = $"Every {ActionHour} at {ActionMinute} past the hour";
            }
            else
            {
                time = $"Occurs at {ActionHour:D2}:{ActionMinute:D2}";
            }

            // Map ActionType to friendly description
            string action = ActionType switch
            {
                0x01 => "Check-In",
                0x02 => "Report",
                0x03 => "Cell Standby",
                0x04 => "Cell Off",
                0x05 => "Transparent Mode",
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
