// 2026-11-12 15:17 CET | GPT-6 | OpenFill: per-task cost/time/stall watchdog.
using System.Diagnostics;
using OpenFill.Core.Agent;

namespace OpenFill.Core.Hosting;

/// <summary>Per-task safeguards; null values leave their corresponding global guard in place.</summary>
public sealed record TaskSafetyLimits(double? MaxCostUsd = null, int? MaxDurationSeconds = null, int? MaxIdleSeconds = null)
{
    public static string? Validate(double? cost, int? duration, int? idle)
    {
        if (cost is { } c && (!double.IsFinite(c) || c < 0.001 || c > 50))
            return "max_cost_usd must be a finite number from 0.001 to 50 USD.";
        if (duration is { } d && (d < 10 || d > 36000))
            return "max_duration_seconds must be from 10 to 36000 seconds.";
        if (idle is { } i && (i < 15 || i > 3600))
            return "max_idle_seconds must be from 15 to 3600 seconds.";
        return null;
    }
}

/// <summary>
/// Independent timer, separate from the agent model/tool loop. Cancels only its own task.
/// Does not call another AI. An in-flight model request can already have consumed tokens.
/// </summary>
public sealed class TaskSafetyWatchdog
{
    private string? _stopReason;
    public string? StopReason => Volatile.Read(ref _stopReason);

    public static string? Evaluate(TaskSafetyLimits limits, double? measuredCostUsd,
        TimeSpan elapsed, TimeSpan idle, bool waitingForHuman)
    {
        if (limits.MaxCostUsd is { } ceiling && measuredCostUsd is { } spent && spent >= ceiling)
            return "max_cost_usd";
        if (limits.MaxDurationSeconds is { } duration && elapsed.TotalSeconds >= duration)
            return "max_duration_seconds";
        if (!waitingForHuman && limits.MaxIdleSeconds is { } idleCap && idle.TotalSeconds >= idleCap)
            return "stalled_no_progress";
        return null;
    }

    public async Task MonitorAsync(TaskSafetyLimits limits, RunMeter meter,
        Func<bool> isWaitingForHuman, CancellationTokenSource task,
        Action<string> onLimit, CancellationToken stopping)
    {
        var started = Stopwatch.GetTimestamp();
        var lastProgress = started;
        var lastSteps = meter.Steps;
        try
        {
            while (!stopping.IsCancellationRequested && !task.IsCancellationRequested)
            {
                await Task.Delay(500, stopping);
                var now = Stopwatch.GetTimestamp();
                var steps = meter.Steps;
                var waiting = isWaitingForHuman();
                if (steps != lastSteps || waiting)
                {
                    lastSteps = steps;
                    lastProgress = now;
                }
                var reason = Evaluate(limits, meter.CostUsd,
                    Stopwatch.GetElapsedTime(started, now),
                    Stopwatch.GetElapsedTime(lastProgress, now), waiting);
                if (reason is null) continue;
                if (Interlocked.CompareExchange(ref _stopReason, reason, null) is null)
                {
                    try { onLimit(reason); } catch { }
                    try { task.Cancel(); } catch (ObjectDisposedException) { }
                }
                return;
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }
}

