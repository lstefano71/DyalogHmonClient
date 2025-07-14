# Product Context: Dyalog.Hmon.Client

_Last reviewed: 2025-07-14 16:48 CEST_

## Problem Statement
Developers building monitoring and diagnostic tools for Dyalog APL interpreters face complexity in managing multiple connections, handling protocol details, and ensuring robust, real-time data flow.

## Solution Overview
Dyalog.Hmon.Client provides a unified, orchestrator-based API that abstracts protocol and connection management, exposing all activity through a single, reactive event stream. This enables rapid development of reliable monitoring tools with minimal boilerplate.

## Intended Users
- Developers building dashboards, logging services, and health-check utilities for Dyalog APL environments.
- Teams requiring robust, scalable monitoring of multiple interpreters.

## User Experience Goals
- Simple, intuitive API surface.
- Minimal setup for managing multiple sessions.
- Strongly-typed, asynchronous event handling.
- Reliable session tracking and error handling.

_Memory bank fully reviewed and confirmed up to date as of 2025-07-14 16:48 CEST._
