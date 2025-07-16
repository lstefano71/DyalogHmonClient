# Product Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:38 CEST_

## Why this project exists
Modern observability platforms rely on OpenTelemetry for unified metrics and tracing. Dyalog APL environments use the HMON protocol for health and event monitoring, but lack direct integration with OTEL. The adapter bridges this gap, enabling organizations to monitor APL systems alongside other infrastructure using standard OTEL tools.

## Problems it solves
- Lack of visibility of Dyalog APL environments in OTEL dashboards.
- Manual, error-prone translation of HMON events/metrics to OTEL format.
- Fragmented monitoring workflows for APL and non-APL systems.
- Difficulty correlating APL events with broader system traces.

## How it should work
- Connects to HMON event/metric streams and translates them to OTEL metrics/traces.
- Provides configurable mapping, filtering, and enrichment of events.
- Forwards data in real-time to OTEL collectors or compatible endpoints.
- Handles errors and connection issues gracefully.

## User experience goals
- Simple configuration and deployment.
- Reliable, low-latency event forwarding.
- Clear diagnostics and logging (Serilog).
- Seamless integration with existing OTEL pipelines.
