# Project Brief: Dyalog.Hmon.Client

## Vision
Deliver a high-quality, modern, and robust .NET library for managing communications with Dyalog APL interpreters via the Health Monitor (HMON) protocol. The library will enable the creation of advanced monitoring and diagnostic tools in C#.

## Purpose
Dyalog.Hmon.Client abstracts the complexities of the HMON protocol, connection management, and data flow, providing a unified, reactive event stream for consumer applications. It is designed to simplify the development of tools that monitor multiple concurrent Dyalog HMON sessions.

## Goals and Objectives

- **Primary Goal:** Provide a session-management library that simplifies building applications monitoring multiple Dyalog HMON sessions.
- **Key Objectives:**
  - Unified session management via a central orchestrator.
  - Resilient, automatically retried connections.
  - Reactive event architecture with a unified, strongly-typed event stream.
  - Stable session identity for reliable state tracking.
  - Intuitive API for interacting with managed interpreters.

## Scope
This project covers the Dyalog.Hmon.Client library only. Client applications (e.g., dashboards, CLI tools) are out of scope.
