namespace AllynTech.MeadowTools.TaskScheduler
{
    public enum CallInAction
    {
        END = 0,
        REPORT_STATUS = 1,
        REPORT_DATA = 2,
        DEVICE_STANDBY = 3,
        DEVICE_OFF = 4,
        TRANSPARENT_MODE = 5
    }

    public enum ScheduleDays
    {
        SUNDAY = 0x01,
        MONDAY = 0x02,
        TUESDAY = 0x04,
        WEDNESDAY = 0x08,
        THURSDAY = 0x10,
        FRIDAY = 0x20,
        SATURDAY = 0x40,
        ALL = 0x7F
    }
}
