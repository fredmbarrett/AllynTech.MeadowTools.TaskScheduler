# AllynTech.MeadowTools.TaskScheduler

The **AllynTech.MeadowTools.TaskScheduler** is an open-source task scheduling library developed by **Allyn Technology Group**.  
It provides a lightweight, high-performance scheduler optimized for constrained environments like the Wilderness Labs Meadow F7, but can be used in any .NET Standard 2.1 project.

## ✨ Features
- Min-heap–driven event loop for efficient scheduling
- Supports per-second, per-minute, hourly, and daily recurrence
- Worker pool with bounded concurrency
- Low memory footprint (optimized for IoT devices)
- Thread-safe operations with minimal overhead
- Extensible factories for creating schedule types:
  - **Call-In schedules**
  - **Data Log schedules**
  - **GNSS lookup schedules**
  - **Sensor Poll schedules**

## 📦 Getting Started

### Requirements
- .NET Standard 2.1 or higher
- Compatible with Meadow.Foundation and Meadow.Core (for Meadow devices)

### Installation
Clone the repository and add the project to your solution:

```bash
git clone https://github.com/fredmbarrett/AllynTech.MeadowTools.TaskScheduler.git
```

Then reference the project in your `.csproj`:

```xml
<ProjectReference Include="path/to/TaskScheduler.csproj" />
```

### Example Usage

```csharp
var scheduler = new SchedulerService();
await scheduler.Start();

var entry = new ScheduleEntry(
    scheduleId: 1,
    scheduleName: "Example Job",
    kind: ScheduleKind.DataLog,
    maxRuntime: TimeSpan.FromSeconds(30),
    nextRunUtc: DateTime.UtcNow.AddSeconds(5),
    work: async (ct) => { Console.WriteLine("Hello from scheduled task!"); },
    computeNext: (last, runtime) => last.AddSeconds(30)
);

scheduler.AddOrReplace(new[] { entry });
```

## 🤝 Contributing

We welcome contributions from the community! To get started:

1. **Fork the repository** and create a new branch for your changes.  
   ```bash
   git checkout -b feature/my-new-feature
   ```
2. **Follow coding style guidelines**  
   - Use [C# standard conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).  
   - Include XML comments for all public members.  
   - Keep methods small and focused.  
3. **Write unit tests** for new features or bug fixes.  
4. **Submit a pull request (PR)** with a clear description of your changes.  

### Reporting Issues
If you find a bug or have a feature request, please open an [issue](https://github.com/fredmbarrett/AllynTech.MeadowTools.TaskScheduler/issues).  
Be sure to include details about your environment and steps to reproduce.

---

## 📜 License
This project is licensed under the **Apache License 2.0** – see the [LICENSE](LICENSE.txt) file for details.  

© 2025 Allyn Technology Group. All rights reserved.

## 🙌 Acknowledgements
Developed as part of the **Allyn Technology Group Meadow Tools** initiative.  
Optimized for use with the Wilderness Labs Meadow F7 and other IoT hardware.
