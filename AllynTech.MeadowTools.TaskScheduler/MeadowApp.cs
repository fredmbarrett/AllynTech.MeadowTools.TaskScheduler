using AllynTech.MeadowTools.TaskScheduler.DataModels;
using AllynTech.MeadowTools.TaskScheduler.Factories;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Leds;
using Meadow.Logging;
using Meadow.Peripherals.Leds;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler
{
    public class MeadowApp : App<F7FeatherV2>
    {
        private static Logger Log => Resolver.Log;

        RgbPwmLed onboardLed;
        private SchedulerService _scheduler;

        public override async Task Initialize()
        {
            Log.LogLevel = LogLevel.Trace;
            Resolver.Log.Info("Initialize...");

            _scheduler = new SchedulerService();
            await _scheduler.Start();

            await CreateCallInSchedule();
            await CreateDataLogSchedule();
            await CreateSensorPollSchedule();

            onboardLed = new RgbPwmLed(
                redPwmPin: Device.Pins.OnboardLedRed,
                greenPwmPin: Device.Pins.OnboardLedGreen,
                bluePwmPin: Device.Pins.OnboardLedBlue,
                CommonType.CommonAnode);

        }

        public override Task Run()
        {
            Resolver.Log.Info("Run...");

            return CycleColors(TimeSpan.FromMilliseconds(1000));
        }

        async Task CycleColors(TimeSpan duration)
        {
            Resolver.Log.Info("Cycle colors...");

            while (true)
            {
                await ShowColorPulse(Color.Blue, duration);
                await ShowColorPulse(Color.Cyan, duration);
                await ShowColorPulse(Color.Green, duration);
                await ShowColorPulse(Color.GreenYellow, duration);
                await ShowColorPulse(Color.Yellow, duration);
                await ShowColorPulse(Color.Orange, duration);
                await ShowColorPulse(Color.OrangeRed, duration);
                await ShowColorPulse(Color.Red, duration);
                await ShowColorPulse(Color.MediumVioletRed, duration);
                await ShowColorPulse(Color.Purple, duration);
                await ShowColorPulse(Color.Magenta, duration);
                await ShowColorPulse(Color.Pink, duration);
            }
        }

        async Task ShowColorPulse(Color color, TimeSpan duration)
        {
            await onboardLed.StartPulse(color, duration / 2);
            await Task.Delay(duration);
        }

        private Task CreateCallInSchedule()
        {
            Log.Trace("Creating simulated call-in schedule...");

            var schedule = new CallInSchedule
            {
                Id = 1,
                ActionDays = 0x7F, // Every day
                ActionHour = 25,   // interval flag
                ActionMinute = 65, // 5-minute intervals
                ActionType = 0x02, // Report sensor data
            };

            var scheduleEntry = CallInScheduleEntryFactory.From(
                schedule,
                async (schedule) =>
                {
                    Log.Info($"Executing call-in schedule for time {DateTime.Now:yyyy-MM-dd HH:mm}: {schedule}");
                    await Task.Delay(3000); // Simulate work
                    //Log.Info("Call-in work complete.");
                });

            _scheduler.AddOrReplace([scheduleEntry]);

            // Method performs no awaited work; return a completed Task.
            return Task.CompletedTask;
        }

        private Task CreateDataLogSchedule()
        {
            Log.Trace("Creating simulated data log schedule...");
            List<DataLogSchedule> schedules = [
                new DataLogSchedule
                {
                    Id = 1,
                    ActionDays = 0x7F, // Every day
                    ActionHour = 25,   // interval flag
                    ActionMinute = 61, // every 1 minute
                    ActionSecond = 0,
                    ActionParam = 1
                },
                new DataLogSchedule
                {
                    Id = 2,
                    ActionDays = 0x7F, // Every day
                    ActionHour = 25,   // interval flag
                    ActionMinute = 62, // every 2 minutes
                    ActionSecond = 0,
                    ActionParam = 2
                }];

            foreach (var schedule in schedules)
            {
                var scheduleEntry = DataLogScheduleFactory.From(
                schedule,
                async (schedule) =>
                {
                    Log.Info($"Executing data log schedule for time {DateTime.Now:yyyy-MM-dd HH:mm}: {schedule}");
                    await Task.Delay(2000); // Simulate work
                    //Log.Info("Data log work complete.");
                });

                _scheduler.AddOrReplace([scheduleEntry]);
            }

            // Method performs no awaited work; return a completed Task.
            return Task.CompletedTask;
        }

        private Task CreateSensorPollSchedule()
        {
            Log.Trace("Creating simulated sensor poll schedule...");
            var schedule = new SensorPollSchedule
            {
                Id = 1,
                ActionDays = 0x7F, // Every day
                ActionHour = 25,   // interval flag
                ActionMinute = 60, // interval flag
                ActionSecond = 61, // every one second
                ActionParam = 1
            };
            var scheduleEntry = SensorPollScheduleFactory.From(
                schedule,
                async (schedule) =>
                {
                    Log.Info($"Executing sensor poll schedule for time {DateTime.Now:yyyy-MM-dd HH:mm}: {schedule}");
                    await Task.Delay(500); // Simulate work
                    //Log.Info("Sensor poll work complete.");
                });

            _scheduler.AddOrReplace([scheduleEntry]);
            // Method performs no awaited work; return a completed Task.
            return Task.CompletedTask;
        }
    }
}