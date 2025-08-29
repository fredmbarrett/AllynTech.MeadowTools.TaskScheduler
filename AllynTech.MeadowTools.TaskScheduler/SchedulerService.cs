// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AllynTech.MeadowTools.TaskScheduler
{
    /// <summary>
    /// Priority-queue based scheduler for periodic and aligned jobs.
    /// Uses a single long-running event loop thread and a bounded worker pool.
    /// </summary>
    public sealed class SchedulerService : MeadowBase, IAsyncDisposable
    {
        private readonly MinHeap<ScheduleEntry> _heap;
        private readonly IComparer<ScheduleEntry> _comparer = new ByNextRunComparer();

        private readonly ConcurrentQueue<ScheduleEntry> _workQ = new ConcurrentQueue<ScheduleEntry>();
        private readonly SemaphoreSlim _workSignal = new SemaphoreSlim(0);

        private readonly ConcurrentQueue<(ScheduleEntry entry, TimeSpan runtime)> _fbQ = new ConcurrentQueue<(ScheduleEntry, TimeSpan)>();
        private readonly SemaphoreSlim _fbSignal = new SemaphoreSlim(0);

        private readonly int _maxParallel;
        private readonly int _boundedCapacity;
        private readonly int _workerCapacity; // number of worker threads (excludes the event-loop thread)
        private int _inFlight = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool IsCancelled => _cts.IsCancellationRequested;
        private readonly object _gate = new object();

        // Lightweight diagnostics
        private readonly TimeSpan _heartbeatPeriod = TimeSpan.FromMinutes(5);
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;
        private static int _runningWork;
        private static int _everStarted;
        private static int _everCompleted;

        public bool IsStarted { get; private set; }

        public SchedulerService(int maxParallel = 4, int boundedCapacity = 32)
        {
            _maxParallel = Math.Max(1, maxParallel);
            _boundedCapacity = Math.Max(1, boundedCapacity);
            _workerCapacity = Math.Max(1, _maxParallel - 1);
            _heap = new MinHeap<ScheduleEntry>(_comparer);

            Log.Debug($"[Scheduler] start: maxParallel={_maxParallel}, workerCapacity={_workerCapacity}, boundedCapacity={_boundedCapacity}");
        }

        /// <summary>
        /// Starts the event loop on a dedicated thread and spins up the worker pool.
        /// Safe to call once.
        /// </summary>
        public async Task Start()
        {
            if (IsStarted) return;

            try
            {
                // Event loop runs synchronously on a long-running thread.
                _ = Task.Factory.StartNew(
                    () => EventLoop(),
                    _cts.Token,
                    TaskCreationOptions.LongRunning,
                    System.Threading.Tasks.TaskScheduler.Default);

                // Workers consume jobs from the queue and report feedback.
                for (int i = 0; i < _workerCapacity; i++)
                {
                    int id = i + 1;
                    _ = Task.Run(() => WorkerLoop(id), _cts.Token);
                }

                await Task.Yield();
                IsStarted = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to start Scheduler Service: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds or replaces schedules by (Kind, ScheduleId). First run times are computed from now.
        /// </summary>
        public void AddOrReplace(IEnumerable<ScheduleEntry> entries)
        {
            if (entries == null) return;

            lock (_gate)
            {
                foreach (var e in entries)
                {
                    _heap.RemoveWhere(x => x.Kind == e.Kind && x.ScheduleId == e.ScheduleId);

                    var next = e.ComputeNext(DateTime.UtcNow, TimeSpan.Zero);
                    e.SetNext(next);
                    _heap.Push(e);

                    Log.Trace($"[Scheduler] Added {e.Kind}:{e.ScheduleId}, next={e.NextRunUtc:yyyy-MM-dd HH:mm:ss}");
                }

                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>Removes all schedules of the specified kind.</summary>
        public void RemoveSchedules(ScheduleKind kind)
        {
            lock (_gate)
            {
                _heap.RemoveWhere(x => x.Kind == kind);
                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>Removes a single schedule matching both Kind and ScheduleId.</summary>
        public void RemoveSchedule(ScheduleEntry entry)
        {
            if (entry == null) return;

            lock (_gate)
            {
                _heap.RemoveWhere(x => x.Kind == entry.Kind && x.ScheduleId == entry.ScheduleId);
                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>Clears work/feedback queues, in-flight count, and all scheduled entries.</summary>
        public void ClearAllSchedules()
        {
            Log.Trace("[Scheduler] ClearAllSchedules");

            lock (_gate)
            {
                while (_workQ.TryDequeue(out _)) { }
                while (_fbQ.TryDequeue(out _)) { }

                _inFlight = 0;
                _heap.Clear();

                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>Cancels the scheduler and releases waiters.</summary>
        public ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _workSignal.Release(int.MaxValue);
            _fbSignal.Release(int.MaxValue);
            IsStarted = false;
            return ValueTaskEx.CompletedTask;
        }

        /// <summary>
        /// Main event loop. Pops due jobs into a buffer, dispatches with capacity limits,
        /// and processes worker feedback to reschedule jobs.
        /// </summary>
        private void EventLoop()
        {
            const int DefaultNapMs = 250;
            Log.Debug($"[Scheduler] Event loop started {DateTime.UtcNow:O}");

            var dueBuffer = new List<ScheduleEntry>(16);

            while (!IsCancelled)
            {
                // Heartbeat
                var nowUtc = DateTime.UtcNow;
                if (nowUtc - _lastHeartbeatUtc >= _heartbeatPeriod)
                {
                    LogHeartbeat();
                    _lastHeartbeatUtc = nowUtc;
                }

                TimeSpan wait = TimeSpan.FromMilliseconds(DefaultNapMs);

                lock (_gate)
                {
                    if (!_heap.Any)
                    {
                        Monitor.Wait(_gate, 100);
                    }

                    var now = DateTime.UtcNow;

                    while (_heap.Any && _heap.Peek().NextRunUtc <= now)
                        dueBuffer.Add(_heap.Pop());

                    if (dueBuffer.Count == 0 && _heap.Any)
                    {
                        var next = _heap.Peek().NextRunUtc;
                        var delta = next - now;
                        wait = (delta > TimeSpan.Zero) ? delta : TimeSpan.Zero;
                    }
                }

                // Dispatch due jobs until capacity is reached.
                while (dueBuffer.Count > 0 && !IsCancelled)
                {
                    if (_workQ.Count >= _boundedCapacity ||
                        Volatile.Read(ref _inFlight) >= _workerCapacity)
                        break;

                    var entry = dueBuffer[dueBuffer.Count - 1];
                    dueBuffer.RemoveAt(dueBuffer.Count - 1);

                    _workQ.Enqueue(entry);
                    Interlocked.Increment(ref _inFlight);
                    _workSignal.Release();
                }

                // Process any worker feedback (rescheduling).
                var waitMs = (int)Math.Min(Math.Max(wait.TotalMilliseconds, 10), 1000);
                if (_fbSignal.Wait(waitMs))
                {
                    while (_fbQ.TryDequeue(out var fb))
                    {
                        var (entry, runtime) = fb;
                        var nextUtc = entry.ComputeNext(entry.NextRunUtc, runtime);

                        lock (_gate)
                        {
                            entry.SetNext(nextUtc);
                            _heap.Push(entry);
                        }
                    }

                    continue; // loop immediately after feedback
                }

                if (dueBuffer.Count > 0) continue; // capacity blocked; retry immediately
            }
        }

        /// <summary>
        /// Worker loop. Executes one job at a time with a per-job runtime timeout,
        /// reports execution time as feedback, and frees capacity reliably.
        /// </summary>
        private async Task WorkerLoop(int id)
        {
            Log.Debug($"[Scheduler] Worker {id} up");

            while (!IsCancelled)
            {
                try
                {
                    await _workSignal.WaitAsync(_cts.Token);
                    if (IsCancelled) break;

                    if (!_workQ.TryDequeue(out var job))
                        continue;

                    var sw = Stopwatch.StartNew();

                    try
                    {
                        var timeout = job.MaxAllowedRuntime;

                        using (var jobCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                        {
                            var jobName = !string.IsNullOrEmpty(job.ScheduleName) ?
                                $"{job.ScheduleName} ({job.ScheduleId}):{job.Kind}" :
                                $"{job.ScheduleId}:{job.Kind}";

                            var runTask = RunWorkTracked(jobName, job.Work, jobCts.Token);

                            var finished = await Task.WhenAny(runTask, Task.Delay(timeout)).ConfigureAwait(false) == runTask;

                            if (!finished)
                            {
                                Log.Warn($"[Scheduler] Job {jobName} exceeded {timeout.TotalSeconds:N0}s; cancelling.");
                                jobCts.Cancel(); // cooperative cancel; underlying I/O should honor it
                                // do not await runTask here; free capacity immediately
                            }
                            else
                            {
                                await runTask.ConfigureAwait(false); // propagate/log exceptions from wrapper
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"[Scheduler] Job {job.Kind}:{job.ScheduleId} error");
                    }
                    finally
                    {
                        var took = sw.Elapsed;
                        _fbQ.Enqueue((job, took));
                        _fbSignal.Release();
                        Interlocked.Decrement(ref _inFlight);
                    }
                }
                catch
                {
                    // Keep worker alive on all exceptions.
                }
            }
        }

        /// <summary>
        /// Executes a single job with basic counters and logging.
        /// </summary>
        private async Task RunWorkTracked(string jobName, Func<CancellationToken, Task> work, CancellationToken ct)
        {
            Interlocked.Increment(ref _everStarted);
            var nowRunning = Interlocked.Increment(ref _runningWork);

            try
            {
                if (nowRunning > _workerCapacity)
                    Log.Warn($"[Scheduler] Active jobs {nowRunning} exceeded worker capacity {_workerCapacity}");

                var timer = Stopwatch.StartNew();
                Log.Trace($"[Scheduler] Starting job {jobName}...");
                await work(ct).ConfigureAwait(false);
                Log.Trace($"[Scheduler] Completed job {jobName} in {timer.GetElapsedSeconds()} seconds.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Log.Debug($"[Scheduler] Job {jobName} cancelled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[Scheduler] Job {jobName} failed.");
            }
            finally
            {
                Interlocked.Decrement(ref _runningWork);
                Interlocked.Increment(ref _everCompleted);
            }
        }

        private void LogHeartbeat()
        {
            var active = Volatile.Read(ref _runningWork);
            var started = Volatile.Read(ref _everStarted);
            var finished = Volatile.Read(ref _everCompleted);
            var queued = _workQ.Count;
            var inflight = Volatile.Read(ref _inFlight);

            Log.Info($"[diag scheduler] running={active} started={started} completed={finished} queued={queued} inflight={inflight}");
        }

        /// <summary>
        /// Orders entries by NextRunUtc, then by Kind, then by ScheduleId.
        /// </summary>
        private sealed class ByNextRunComparer : IComparer<ScheduleEntry>
        {
            public int Compare(ScheduleEntry a, ScheduleEntry b)
            {
                int c = DateTime.Compare(a.NextRunUtc, b.NextRunUtc);
                if (c != 0) return c;

                c = a.Kind.CompareTo(b.Kind);
                if (c != 0) return c;

                return a.ScheduleId.CompareTo(b.ScheduleId);
            }
        }
    }
}
