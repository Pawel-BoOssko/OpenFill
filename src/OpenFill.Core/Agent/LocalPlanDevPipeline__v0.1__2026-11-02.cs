// OF-000013 / 2026-11-04: bounded plan execution compiled in Release.
// No direct model calls, no send/save/submit, production requires explicit opt-in.
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record LocalPlanDevTelemetry(
    string BusinessOperationId, string TaskId, string RunId, string Protocol,
    string Status, string? Reason, int ModelCalls, int SnapshotCalls, int SnapshotSuccesses,
    int NavigationAttempts, int NavigationApplied, int FillAttempts, int FillApplied,
    int ObservedConditions, int ForeachItems, long ElapsedMs)
{
    public int BrowserActionAttempts => NavigationAttempts + FillAttempts;
    public int BrowserActionsApplied => NavigationApplied + FillApplied;
}

public sealed class LocalPlanDevPipeline
{
    private sealed class Meter
    {
        public int SnapshotCalls, SnapshotSuccesses;
        public int NavigationAttempts, NavigationApplied, FillAttempts, FillApplied;
    }

    private class MeasuredBrowser : ILocalPlanBrowser
    {
        private readonly ILocalPlanBrowser _inner;
        protected readonly Meter Meter;

        public MeasuredBrowser(ILocalPlanBrowser inner, Meter meter)
        { _inner = inner; Meter = meter; }

        public async Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
        {
            Meter.SnapshotCalls++;
            var snapshot = await _inner.ReadAsync(ct);
            Meter.SnapshotSuccesses++;
            return snapshot;
        }

        public async Task<SemanticPlanClickResult> ClickNavigationAsync(
            string elementRef, long expectedRevision, CancellationToken ct)
        {
            Meter.NavigationAttempts++;
            var result = await _inner.ClickNavigationAsync(elementRef, expectedRevision, ct);
            if (result.Status == "applied") Meter.NavigationApplied++;
            return result;
        }
    }

    private sealed class MeasuredFormBrowser : MeasuredBrowser, ILocalPlanFormBrowser
    {
        private readonly ILocalPlanFormBrowser _inner;
        public MeasuredFormBrowser(ILocalPlanFormBrowser inner, Meter meter)
            : base(inner, meter) => _inner = inner;

        public async Task<SemanticPlanClickResult> FillTextAsync(
            string elementRef, long expectedRevision, string value, CancellationToken ct)
        {
            Meter.FillAttempts++;
            var result = await _inner.FillTextAsync(elementRef, expectedRevision, value, ct);
            if (result.Status == "applied") Meter.FillApplied++;
            return result;
        }
    }

    public async Task<LocalPlanDevTelemetry> ExecuteAsync(
        JsonObject request, ILocalPlanBrowser browser,
        string businessOperationId, string taskId, string runId,
        CancellationToken ct = default)
    {
        static void ValidId(string id, string key)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentException("dev_invalid_" + key);
        }

        ValidId(businessOperationId, "business_operation_id");
        ValidId(taskId, "task_id");
        ValidId(runId, "run_id");
        var watch = Stopwatch.StartNew();
        var meter = new Meter();
        var measured = browser is ILocalPlanFormBrowser form
            ? (ILocalPlanBrowser)new MeasuredFormBrowser(form, meter)
            : new MeasuredBrowser(browser, meter);
        var status = "invalid_plan";
        string? reason = null;
        string protocol = request["protocol"]?.ToString() ?? "";
        var observed = 0;
        var iterations = 0;

        try
        {
            if (protocol == "of.plan.v1")
            {
                var plan = PlanProtocolValidator.Parse(request);
                var outcome = await new LocalPlanExecutor().ExecuteAsync(plan, measured, ct);
                status = outcome.Status;
                reason = outcome.Reason;
                observed = outcome.Nodes.Count(n => n.Status == "observed");
            }
            else if (protocol == "of.flow.v1")
            {
                LocalPlanControlFlow.Validate(request);
                var expectedHost = request["expected_host"]!.GetValue<string>();
                var outcome = await new LocalPlanControlFlow(measured, expectedHost)
                    .ExecuteAsync(request, ct);
                status = outcome.Status;
                reason = outcome.Reason;
                observed = outcome.Observed.Count;
                iterations = outcome.Iterations;
            }
            else reason = "dev_protocol_unsupported";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ArgumentException ex) { reason = ex.Message; }
        // Genuine adapter errors must reach the caller, not be reported as successful plans.

        watch.Stop();
        return new LocalPlanDevTelemetry(
            businessOperationId, taskId, runId, protocol, status, reason,
            ModelCalls: 0, meter.SnapshotCalls, meter.SnapshotSuccesses,
            meter.NavigationAttempts, meter.NavigationApplied,
            meter.FillAttempts, meter.FillApplied, observed, iterations,
            watch.ElapsedMilliseconds);
    }
}
