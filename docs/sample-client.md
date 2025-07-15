# Sample Client Overview

This document describes the sample client provided in `Dyalog.Hmon.Client.Console/Program.cs`. The sample client demonstrates how to use the Dyalog.Hmon.Client library to monitor and interact with Dyalog APL servers.

## Purpose

The sample client acts as a monitoring service for Dyalog APL sessions, displaying live session facts and events in a terminal UI using Spectre.Console.

## Main Features

- **Server Discovery & Connection:** Listens for incoming Dyalog HMON server connections on a configurable port (default: 8080).
- **Session State Tracking:** Maintains state for each connected session, including workspace, thread, host, and account information.
- **Event Handling:** Subscribes to server events and updates session state in response to facts and notifications.
- **Live Terminal Dashboard:** Uses Spectre.Console to render a live-updating table of connected sessions and their key facts, with carousel-style fact display.
- **Graceful Shutdown:** Supports cancellation and clean resource disposal.

## Workflow

1. **Startup:** The client starts a listener for incoming server connections.
2. **Session Management:** On connection, the client subscribes to relevant events and starts polling for session facts.
3. **Event Processing:** All incoming events are processed to update the session state and recent activity.
4. **Live Display:** The terminal UI updates every second, showing session IDs, names, and a rotating view of session facts.
5. **Shutdown:** On user input, the client cancels all operations and disposes resources.

## Technologies Used

- **Dyalog.Hmon.Client.Lib:** Core library for HMON protocol and session management.
- **Spectre.Console:** For rich terminal UI rendering.
- **.NET 9.0 / C# 13:** Modern language features and async workflows.

## Usage

1. Build and run the console project.
2. Connect one or more Dyalog HMON servers to the listening port.
3. Observe live session data and events in the terminal.

Refer to the source code in `Dyalog.Hmon.Client.Console/Program.cs` for implementation details.
