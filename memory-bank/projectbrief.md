# Project Brief: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:38 CEST_

## Vision
Deliver a robust, modern .NET adapter that translates Dyalog HMON protocol events and metrics into OpenTelemetry-compatible data, enabling seamless integration of APL monitoring into OTEL-based observability platforms.

## Purpose
The HMON-to-OTEL Adapter abstracts the complexities of HMON event and metric formats, providing a reliable bridge to OpenTelemetry. It enables real-time monitoring, diagnostics, and analytics for Dyalog APL environments using industry-standard OTEL tools.

## Goals and Objectives

- **Primary Goal:** Provide an adapter that translates HMON events and metrics to OpenTelemetry format for ingestion by OTEL collectors and observability platforms.
- **Key Objectives:**
  - Accurate mapping of HMON protocol data to OTEL metrics and traces.
  - Real-time event forwarding with minimal latency.
  - Configurable mapping and filtering of events/metrics.
  - Resilient error handling and connection management.
  - Easy integration into existing OTEL pipelines.

## Scope
This project covers the HMON-to-OTEL Adapter only. Client applications and HMON server implementations are out of scope.

_Memory bank fully reviewed and confirmed up to date as of 2025-07-16 09:38 CEST._
