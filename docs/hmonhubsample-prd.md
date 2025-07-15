# Product Requirements Document: HMon Hub Sample

## 1. Overview

This document outlines the requirements for a new sample client for the Dyalog.Hmon.Client library. This client will be a configuration-driven console application designed to monitor multiple HMon servers simultaneously. It will aggregate monitoring data ("facts") from these servers and expose this data in real-time through a unified API offering both REST and WebSocket access.

The primary goal is to create a robust, flexible monitoring hub that can be easily configured and deployed to observe a dynamic environment of Dyalog APL instances.

## 2. Goals and Objectives

*   **Configuration-Driven:** The client's entire behavior, including server connections and exposed endpoints, must be controlled by a single configuration file.
*   **Data Aggregation:** Collect and maintain a unified, dynamic table of all facts from all connected HMon servers.
*   **Real-time Data Exposure:** Provide access to the aggregated data through both a request/response REST API and a push-based WebSocket feed.
*   **Resilience:** The client should be robust against connection failures, gracefully handling unavailable servers and attempting to reconnect as appropriate.
*   **Simplicity:** The configuration and operation of the client should be straightforward for developers and system administrators.

## 3. Core Features

### 3.1. Configuration File

The application will be driven by a JSON configuration file, `config.json`.

*   **Schema:**
    ```json
    {
      "hmonServers": [
        {
          "name": "ServerA",
          "host": "localhost",
          "port": 12345
        },
        {
          "name": "ServerB",
          "host": "remote.server.com",
          "port": 54321
        }
      ],
      "pollListener": {
        "ip": "0.0.0.0",
        "port": 4501
      },
      "api": {
        "ip": "0.0.0.0",
        "port": 8080
      }
    }
    ```
*   **Details:**
    *   `hmonServers`: An array of HMon servers to connect to. This array can be empty.
        *   `name`: A unique identifier for the server.
        *   `host`, `port`: Connection details for the HMon server.
    *   `pollListener`: An optional object specifying the IP and port for the client to listen on for connections from HMon servers.
    *   `api`: Specifies the IP and port where the client will expose its API.

### 3.2. HMon Server Connectivity

*   The client will parse the `hmonServers` array and attempt to establish a session with each server.
*   If `pollListener` is configured, the client will also listen for incoming connections from HMon servers.
*   The client will log connection successes and failures.
*   It will maintain a connection status for each configured and connected server.

### 3.3. Data Aggregation

*   The client will maintain an in-memory, dynamic table of all facts received from all active HMon sessions.
*   Each record in the table will contain:
    *   The server name (from the config, or derived from the connection if polled).
    *   The session ID.
    *   The fact name (e.g., `cpuUsage`, `memory`, `aplVersion`).
    *   The fact value.
    *   A timestamp of the last update.

### 3.4. REST API

The client will host a RESTful API based on the `api` configuration.

*   **Endpoint:** `GET /facts`
    *   **Response:** Returns the entire current state of the aggregated facts table as a JSON object.
    ```json
    {
      "serverName": "ServerA",
      "sessionId": "...",
      "facts": [
        { "name": "aplVersion", "value": "18.2", "lastUpdate": "2025-07-15T10:30:00Z" },
        { "name": "cpuUsage", "value": "15%", "lastUpdate": "2025-07-15T10:30:05Z" }
      ]
    }
    ```
*   **Endpoint:** `GET /status`
    *   **Response:** Returns the connection status of all configured HMon servers.
    ```json
    [
        { "name": "ServerA", "status": "Connected" },
        { "name": "ServerB", "status": "Disconnected" }
    ]
    ```

### 3.5. WebSocket API

The client will provide a WebSocket endpoint for real-time updates at the `/ws` path on the same host and port as the REST API.

*   **Connection:** Clients can establish a persistent WebSocket connection to the address specified in `api` configuration, at the `/ws` path.
*   **Initial State:** Upon successful connection, the server will immediately send a message of type `snapshot` containing the complete current table of facts.
    *   **Format:**
        ```json
        {
          "type": "snapshot",
          "payload": { ... "facts": [ ... ] ... }
        }
        ```
*   **Live Updates:** As the client receives new or updated facts from any HMon server, it will push a message of type `update` to all connected WebSocket clients.
    *   **Format:**
        ```json
        {
          "type": "update",
          "payload": {
            "serverName": "ServerA",
            "sessionId": "...",
            "fact": { "name": "cpuUsage", "value": "16%", "lastUpdate": "2025-07-15T10:30:10Z" }
          }
        }
        ```

## 4. Non-Functional Requirements

*   **Logging:** The application must provide clear and structured logging for diagnostics, including connection events, API requests, and errors.
*   **Performance:** The client should handle connections to at least a dozen servers and serve dozens of API/WebSocket clients concurrently without significant performance degradation.
*   **Reliability:** The application should run continuously and be able to recover from transient network errors.

## 5. Out of Scope

*   A graphical user interface (GUI).
*   Persistent storage of facts across application restarts. The data table is dynamic and in-memory only.
*   Authentication or authorization for the APIs.
*   Configuration of the client via the API (read-only).
