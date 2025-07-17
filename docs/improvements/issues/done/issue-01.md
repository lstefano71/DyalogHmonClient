# Issue #1: Bug: OTEL Adapter uses `ObservableGauge` for cumulative metrics

**Labels:** `bug`, `high-priority`, `otel-adapter`

## Problem

The `Dyalog.Hmon.OtelAdapter.AdapterService` currently reports all metrics, including cumulative ones like `WorkspaceFact.Compactions` and `AccountInformationFact.ComputeTime`, using `ObservableGauge`. A gauge represents a single, point-in-time value. This is incorrect for values that only ever increase.

## Impact

Monitoring platforms (Prometheus, Grafana, etc.) will misinterpret these metrics. They cannot calculate rates or display them as ever-increasing counters, leading to fundamentally incorrect observability data.

## Proposed Solution

1. Identify all metrics in the PRD (`docs/hmon-to-otel-adapter-PRD.md`) that are specified as `Counter`.
2. In `AdapterService.cs`, change the instrument creation for these metrics from `_meter.CreateObservableGauge(...)` to `_meter.CreateObservableCounter(...)`.
3. Adjust the measurement logic accordingly. An `ObservableCounter` reports the total accumulated value.

## Acceptance Criteria

- Metrics like `dyalog.workspace.compactions` and `dyalog.cpu.time` must be exported as OTel `Sum` (Counter) types.
- Verification can be done by inspecting the output of the OTel `debug` exporter or by querying the metric in a tool like Prometheus.
