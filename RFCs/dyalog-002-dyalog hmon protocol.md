# Dyalog Health Monitor (HMON) Protocol

**Category:** Informational  
**Author:** Gemini  
**Version:** 1.0  
**Created:** July 14, 2025

## Abstract

This document specifies the Dyalog Health Monitor (HMON) protocol, an application-layer protocol for monitoring the state, performance, and health of a Dyalog APL interpreter. HMON operates over the Dyalog Remote Protocol - Transport (DRP-T), as defined in RFC 0001. It enables a client to query an interpreter for facts about its state, subscribe to notifications about specific events, and manage its availability for remote debugging.

## Status of This Memo

This document provides information for the Internet community. It does not specify an Internet standard of any kind. Distribution of this memo is unlimited.

## 1. Introduction

The HMON protocol provides a structured mechanism for external monitoring tools to communicate with a Dyalog APL interpreter. This allows for the observation of runtime metrics such as memory usage, thread activity, and performance characteristics, as well as receiving notifications for critical events like unhandled exceptions.

This specification defines the syntax and semantics of HMON messages. It assumes a DRP-T session has been successfully established between the client (the monitoring tool) and the interpreter.

### 1.1. Terminology

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in RFC 2119.

* **Client:** The monitoring application that initiates requests and consumes data.
* **Interpreter:** The Dyalog APL process that acts as the logical server, responding to requests and providing data.

## 2. Transport

The HMON protocol operates exclusively over a Dyalog Remote Protocol - Transport (DRP-T) session, as specified in RFC 0001.

* **Magic Number:** The Magic Number field in all HMON DRP-T frames MUST be the 4-octet ASCII sequence "HMON" (`0x48 0x4D 0x4F 0x4E`).

## 3. Message Format

All HMON messages are transported as the payload of a DRP-T frame. The payload MUST be a UTF-8 encoded JSON array containing exactly two elements:

1. A `MessageName` (String) that identifies the command or response.
2. An `Arguments` (Object) that contains key-value pairs specific to the message.

> **Warning:**  
> All boolean properties in HMON JSON payloads MUST be serialized as integers: `0` for `false` and `1` for `true`. Implementers should ensure this convention is followed to maintain compatibility with the protocol.

Example: `["MessageName", {"arg1": "value1", "arg2": 123}]`

All message and argument names are case-sensitive.

## 4. Common Arguments

* **UID:** Most client requests MAY include a "UID" (String) field in the arguments object. If a UID is present in a request, the corresponding response from the interpreter MUST include the identical UID value. This allows clients to correlate requests with responses.

## 5. Client-to-Interpreter Messages (Requests)

The following messages are sent from the Client to the Interpreter.

### 5.1. GetFacts

Requests a one-time snapshot of one or more "facts" about the interpreter's state.

* **Example:** `["GetFacts", {"Facts": ["Host", "Workspace"], "UID": "req-001"}]`
* **Arguments:**

    | Name    | Type                       | Required | Description                                                                                                                              |
    | :------ | :------------------------- | :------- | :--------------------------------------------------------------------------------------------------------------------------------------- |
    | `Facts` | Array of (String or Number) | Yes      | An array of fact identifiers to be retrieved. If the array is empty, all available facts will be returned. See Section 8.1 for identifiers. |
    | `UID`   | String                     | No       | A user-defined unique identifier for the request.                                                                                        |

### 5.2. PollFacts

Requests that the interpreter send `Facts` messages repeatedly at a given interval.

* **Example:** `["PollFacts", {"Facts": [3, 6], "Interval": 5000, "UID": "poll-mem"}]`
* **Arguments:**

    | Name       | Type                       | Required | Description                                                                                                                               |
    | :--------- | :------------------------- | :------- | :---------------------------------------------------------------------------------------------------------------------------------------- |
    | `Facts`    | Array of (String or Number) | Yes      | An array of fact identifiers to be polled. If the array is empty, all available facts will be polled. See Section 8.1 for identifiers.      |
    | `Interval` | Number                     | No       | The polling interval in milliseconds. If omitted, defaults to 1000ms. Values less than 500ms will be treated as 500ms.                   |
    | `UID`      | String                     | No       | A user-defined unique identifier for the request, which will be echoed in every polled `Facts` response.                                  |

### 5.3. StopFacts

Cancels any active `PollFacts` request.

* **Example:** `["StopFacts", {}]`
* **Arguments:** The arguments object MUST be empty. A `UID` MUST NOT be included.

### 5.4. BumpFacts

Triggers an immediate `Facts` message from an active `PollFacts` poll, without resetting the polling interval.

* **Example:** `["BumpFacts", {}]`
* **Arguments:** The arguments object MUST be empty. A `UID` MUST NOT be included.

### 5.5. Subscribe

Subscribes the client to receive `Notification` messages when specific events occur in the interpreter. Sending a new `Subscribe` message replaces any existing subscriptions.

* **Example:** `["Subscribe", {"Events": ["UntrappedSignal"], "UID": "sub-errors"}]`
* **Arguments:**

    | Name     | Type                       | Required | Description                                                                                                                           |
    | :------- | :------------------------- | :------- | :------------------------------------------------------------------------------------------------------------------------------------ |
    | `Events` | Array of (String or Number) | Yes      | An array of event identifiers to subscribe to. An empty array unsubscribes from all events. See Section 8.2 for identifiers. |
    | `UID`    | String                     | No       | A user-defined identifier that will be echoed in the `Subscribed` response and all subsequent `Notification` messages.             |

### 5.6. GetLastKnownState

Requests a high-priority status report from the interpreter. This is intended for diagnostic purposes when the interpreter may be unresponsive to other requests.

* **Example:** `["GetLastKnownState", {"UID": "check-liveness"}]`
* **Arguments:**

    | Name  | Type   | Required | Description                                       |
    | :---- | :----- | :------- | :------------------------------------------------ |
    | `UID` | String | No       | A user-defined unique identifier for the request. |

### 5.7. ConnectRide

Requests that the interpreter connect to or disconnect from a RIDE client. This command requires Access Level 3 to be configured on the interpreter.

* **Example (Connect):** `["ConnectRide", {"Address": "localhost", "Port": 4502}]`
* **Example (Disconnect):** `["ConnectRide", {}]`
* **Arguments:**

    | Name      | Type   | Required                         | Description                                       |
    | :-------- | :----- | :------------------------------- | :------------------------------------------------ |
    | `Address` | String | Yes (for connection requests)    | The address of the RIDE instance to connect to.   |
    | `Port`    | Number | Yes (for connection requests)    | The port of the RIDE instance.                    |
    | `UID`     | String | No                               | A user-defined unique identifier for the request. |

If `Address` and `Port` are both present and valid, a connection will be attempted. If either is missing or invalid, any existing RIDE connection will be disconnected.

#### 6. Interpreter-to-Client Messages (Responses)

The following messages are sent from the Interpreter to the Client.

#### 6.1. Facts

The response to `GetFacts`, `PollFacts`, `StopFacts`, or `BumpFacts`. Contains the requested information.

* **Example:** `["Facts", {"UID": "req-001", "Interval": 5000, "Facts": [{"ID": 6, "Name": "ThreadCount", "Value": {"Total": 1, "Suspended": 0}}]}]`
* **Arguments:**

    | Name       | Type           | Required                                      | Description                                                                                                                                  |
    | :--------- | :------------- | :-------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------- |
    | `UID`      | String         | No                                            | The UID from the original request, if provided.                                                                                              |
    | `Interval` | Number         | Yes (for `PollFacts` responses)                 | The polling interval in milliseconds. Present only if this is a response to a polling request. A value of `0` indicates polling has stopped. |
    | `Facts`    | Array of `Fact` | Yes                                           | An array of `Fact` objects. See Section 7.1 for the detailed structure of these objects.                                                     |

### 6.2. Subscribed

The response to a `Subscribe` request, confirming the current subscription status for all subscribable events.

* **Example:** `["Subscribed", {"UID": "sub-errors", "Events": [{"ID": 1, "Name": "WorkspaceCompaction", "Value": 1}]}]`
* **Arguments:**

    | Name     | Type                        | Required | Description                                                                                |
    | :------- | :-------------------------- | :------- | :----------------------------------------------------------------------------------------- |
    | `UID`    | String                      | No       | The UID from the original request, if provided.                                            |
    | `Events` | Array of `SubscriptionStatus` | Yes      | An array of `SubscriptionStatus` objects, one for each subscribable event. See Section 7.7. |

### 6.3. Notification

Sent when a subscribed event occurs. The structure of the `Event` object's payload varies by event type.

* **Example (WorkspaceResize):** `["Notification", {"UID": "sub-errors", "Event": {"ID": 2, "Name": "WorkspaceResize"}, "Size": 894213}]`
* **Arguments:**

    | Name    | Type         | Required | Description                                                          |
    | :------ | :----------- | :------- | :------------------------------------------------------------------- |
    | `UID`   | String       | No       | The UID from the original `Subscribe` request, if provided.          |
    | `Event` | `EventInfo` | Yes      | An `EventInfo` object identifying the event. See Section 7.8.        |

#### 6.3.1. WorkspaceCompaction Event

In addition to the base `Notification` arguments, the following are included:

| Name    | Type                | Description                                     |
| :------ | :------------------ | :---------------------------------------------- |
| `Tid`   | Number              | The ID of the thread where the compaction occurred. |
| `Stack` | Array of `StackInfo` | The execution stack of the thread. See Section 7.5. |

#### 6.3.2. WorkspaceResize Event

In addition to the base `Notification` arguments, the following is included:

| Name   | Type   | Description            |
| :----- | :----- | :--------------------- |
| `Size` | Number | The new workspace size in bytes. |

#### 6.3.3. UntrappedSignal and TrappedSignal Events

In addition to the base `Notification` arguments, the following are included:

| Name        | Type                 | Description                                                        |
| :---------- | :------------------- | :----------------------------------------------------------------- |
| `Tid`       | Number               | The ID of the thread where the signal occurred.                    |
| `Stack`     | Array of `StackInfo` | The execution stack of the thread. See Section 7.5.                |
| `DMX`       | `DmxInfo` or `null`  | An object containing `⎕DMX` information for the thread. See Section 7.5. |
| `Exception` | `ExceptionInfo` or `null` | An object containing `⎕EXCEPTION` information. See Section 7.5.    |

### 6.4. LastKnownState

The response to a `GetLastKnownState` request.

* **Example:** `["LastKnownState", {"UID": "check-liveness", "TS": "20230111T144700.132Z"}]`
* **Arguments:**

    | Name       | Type           | Required | Description                                                                                                                                                                 |
    | :--------- | :------------- | :------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
    | `UID`      | String         | No       | The UID from the original request, if provided.                                                                                                                             |
    | `TS`       | String         | Yes      | The interpreter's current UTC time in ISO 8601 format with millisecond precision (e.g., `YYYY-MM-DDTHH:mm:ss.fffZ`).                                                         |
    | `Activity` | `ActivityInfo` | No       | The interpreter's last recorded activity and timestamp, if event gathering is enabled. See Section 7.9.                                                                       |
    | `Location` | `LocationInfo` | No       | The last executed line of code and timestamp, if event gathering and `⎕PROFILE` are enabled. See Section 7.9.                                                               |
    | `WS FULL`  | `WsFullInfo`   | No       | The timestamp of the last WSFULL event, if event gathering is enabled. Note the key contains a space. See Section 7.9.                                                                |

### 6.5. RideConnection

The response to a `ConnectRide` request.

* **Example:** `["RideConnection", {"UID": "my-uid", "Restricted": 0, "Connect": 1, "Status": 0}]`
* **Arguments:**

    | Name         | Type    | Required                      | Description                                                                                                   |
    | :----------- | :------ | :---------------------------- | :------------------------------------------------------------------------------------------------------------ |
    | `UID`        | String  | No                            | The UID from the original request, if provided.                                                               |
    | `Restricted` | Boolean | Yes                           | `true` if the request was disallowed due to the interpreter's Access Level setting.                             |
    | `Connect`    | Boolean | No (present if `Restricted` is `false`) | `true` for a connection attempt, `false` for a disconnection attempt.                                   |
    | `Status`     | Number  | No (present if `Restricted` is `false`) | The return code from the interpreter's `3502⌶` call, where `0` indicates success.                         |

### 6.6. UserMessage

An application-defined message sent from the interpreter via the `111⌶` system function.

* **Example:** `["UserMessage", {"UID": "app-alert", "Message": "Task completed"}]`
* **Arguments:**

    | Name      | Type         | Required | Description                                                                    |
    | :-------- | :----------- | :------- | :----------------------------------------------------------------------------- |
    | `UID`     | String       | No       | The UID provided to `111⌶`, if any.                                            |
    | `Message` | Any JSON value | Yes      | The message payload provided to `111⌶`, which can be a string, number, or object. |

### 6.7. Error Messages

| MessageName        | Arguments                               | Description                                                                 |
| :----------------- | :-------------------------------------- | :-------------------------------------------------------------------------- |
| `InvalidSyntax`    | `{}`                                    | The request was not syntactically valid JSON.                               |
| `DisallowedUID`    | `{"UID": String, "Name": String}`       | A `UID` was provided in a request that MUST NOT contain one (e.g., `StopFacts`). |
| `UnknownCommand`   | `{"UID": String, "Name": String}`       | The request `MessageName` is not recognized by the interpreter.             |
| `MalformedCommand` | `{"UID": String, "Name": String}`       | The request arguments did not conform to the specification.                 |

## 7. Data Structures

This section defines the structure of complex objects used within HMON messages.

### 7.1. The `Fact` Object

A `Fact` object is a container for a piece of information about the interpreter. It is always an element in the `Facts` array of a `Facts` response.

* **Structure:**

    | Name      | Type                   | Description                                                                   |
    | :-------- | :--------------------- | :---------------------------------------------------------------------------- |
    | `ID`      | Number                 | The numeric identifier for the fact. See Section 8.1.                         |
    | `Name`    | String                 | The string identifier for the fact. See Section 8.1.                          |
    | `Value`   | Object                 | The fact's data. Present for facts that return a single object.               |
    | `Values`  | Array of Objects       | The fact's data. Present for facts that return a list of objects (e.g., Threads). |

### 7.2. The "Host" Fact

Contains information about the host machine, interpreter, and communication layers.

* **`Value` Object Structure:**

    | Name          | Type              | Description                                        |
    | :------------ | :---------------- | :------------------------------------------------- |
    | `Machine`     | `MachineInfo`     | Information about the host machine.                |
    | `Interpreter` | `InterpreterInfo` | Information about the Dyalog interpreter.          |
    | `CommsLayer`  | `CommsLayerInfo`  | Information about the HMON communications layer.   |
    | `RIDE`        | `RideInfo`        | Information about the RIDE communications layer.   |

* **`MachineInfo` Object:**

    | Name          | Type           | Description                                                                |
    | :------------ | :------------- | :------------------------------------------------------------------------- |
    | `Name`        | String         | The name of the machine.                                                   |
    | `User`        | String         | The name of the user account running the interpreter.                      |
    | `PID`         | Number         | The interpreter's process ID.                                              |
    | `Desc`        | Any JSON value | An application-specific description, set by `110⌶`.                         |
    | `AccessLevel` | Number         | The current HMON Access Level (0-3). See Section 8.3.                      |

* **`InterpreterInfo` Object:**

    | Name          | Type    | Description                                                          |
    | :------------ | :------ | :------------------------------------------------------------------- |
    | `Version`     | String  | The interpreter version, in the form "A.B.C".                          |
    | `BitWidth`    | Number  | The interpreter word size, either 32 or 64.                          |
    | `IsUnicode`   | Boolean | `true` if the interpreter is a Unicode edition.                        |
    | `IsRuntime`   | Boolean | `true` if the interpreter is a Runtime edition.                        |
    | `SessionUUID` | String  | A unique Session UUID. `null` if not supported by interpreter version. |

* **`CommsLayerInfo` and `RideInfo` Objects:**

    | Name         | Type    | Description                                                                 |
    | :----------- | :------ | :-------------------------------------------------------------------------- |
    | `Listening`  | Boolean | (`RideInfo` only) `true` if the interpreter is listening for RIDE connections.  |
    | `HTTPServer` | Boolean | (`RideInfo` only) `true` for a "Zero footprint" RIDE server.                  |
    | `Version`    | String  | The Comms Layer (Conga) version.                                            |
    | `Address`    | String  | The interpreter's network IP address.                                       |
    | `Port4`      | Number  | The interpreter's network port number.                                      |
    | `Port6`      | Number  | An alternate port number.                                                   |

### 7.3. The "AccountInformation" Fact

Contains elements from `⎕AI`.

* **`Value` Object Structure:**

    | Name                 | Type   | Description      |
    | :------------------- | :----- | :--------------- |
    | `UserIdentification` | String | User's name.     |
    | `ComputeTime`        | Number | Compute time.    |
    | `ConnectTime`        | Number | Connect time.    |
    | `KeyingTime`         | Number | Keying time.     |

### 7.4. The "Workspace" Fact

Contains statistics from `2000⌶`.

* **`Value` Object Structure:**

    | Name                | Type   | Description                       |
    | :------------------ | :----- | :-------------------------------- |
    | `WSID`              | String | The workspace name.               |
    | `Available`         | Number | Total bytes available.            |
    | `Used`              | Number | Bytes in use.                     |
    | `Compactions`       | Number | Number of compactions.            |
    | `GarbageCollections`| Number | Number of garbage collections.    |
    | `GarbagePockets`    | Number | Number of garbage pockets.        |
    | `FreePockets`       | Number | Number of free pockets.           |
    | `UsedPockets`       | Number | Number of used pockets.           |
    | `Sediment`          | Number | Sediment in bytes.                |
    | `Allocation`        | Number | Current memory allocation in bytes. |
    | `AllocationHWM`     | Number | High water mark for allocation.   |
    | `TrapReserveWanted` | Number | Requested trap reserve in bytes.  |
    | `TrapReserveActual` | Number | Actual trap reserve in bytes.     |

### 7.5. "Threads" and "SuspendedThreads" Facts

These facts return a `Values` array, where each element is a `ThreadInfo` object.

* **`ThreadInfo` Object:**

    | Name        | Type                 | Description                                                                                               |
    | :---------- | :------------------- | :-------------------------------------------------------------------------------------------------------- |
    | `Tid`       | Number               | The thread ID.                                                                                            |
    | `Stack`     | Array of `StackInfo` | The execution stack.                                                                                      |
    | `Suspended` | Boolean              | `true` if the thread is suspended. Omitted from the `SuspendedThreads` fact.                              |
    | `State`     | String               | A string indicating the thread's current location.                                                        |
    | `Flags`     | String               | A string indicating the thread's flags (e.g., "Normal", "Paused").                                        |
    | `DMX`       | `DmxInfo` or `null`  | `⎕DMX` information, present only if the thread is suspended (`Suspended` is `true`).                      |
    | `Exception` | `ExceptionInfo` or `null` | `⎕EXCEPTION` information, present only if the thread is suspended (`Suspended` is `true`).             |

* **`StackInfo` Object:**

    | Name          | Type    | Description                                                                     |
    | :------------ | :------ | :------------------------------------------------------------------------------ |
    | `Restricted`  | Boolean | `true` if some information is missing due to the Access Level.                    |
    | `Description` | String  | A line of SIstack information. Present only if `Restricted` is `false`.         |

* **`DmxInfo` Object:**

    | Name               | Type    | Description                                                              |
    | :----------------- | :------ | :----------------------------------------------------------------------- |
    | `Restricted`       | Boolean | `true` if some information is missing due to the Access Level.           |
    | `Category`         | String  | (`Restricted`: `false`) Error category.                                  |
    | `DM`               | String  | (`Restricted`: `false`) Diagnostic Message.                              |
    | `EM`               | String  | (`Restricted`: `false`) Error Message.                                   |
    | `EN`               | Number  | (`Restricted`: `false`) Error Number.                                    |
    | `ENX`              | String  | (`Restricted`: `false`) Error Number extended.                           |
    | `InternalLocation` | String  | (`Restricted`: `false`) Internal location.                               |
    | `Vendor`           | String  | (`Restricted`: `false`) Vendor.                                          |
    | `Message`          | String  | (`Restricted`: `false`) Message.                                         |
    | `OSError`          | Number  | (`Restricted`: `false`) Operating System error number.                   |

* **`ExceptionInfo` Object:**

    | Name         | Type    | Description                                                           |
    | :----------- | :------ | :-------------------------------------------------------------------- |
    | `Restricted` | Boolean | `true` if some information is missing due to the Access Level.        |
    | `Source`     | Object  | (`Restricted`: `false`) The `Source` property of `⎕EXCEPTION`.          |
    | `StackTrace` | String  | (`Restricted`: `false`) The `StackTrace` property of `⎕EXCEPTION`.      |
    | `Message`    | String  | (`Restricted`: `false`) The `Message` property of `⎕EXCEPTION`.         |

## 7.6. The "ThreadCount" Fact

Contains a summary of the number of threads.

* **`Value` Object Structure:**

    | Name        | Type   | Description                          |
    | :---------- | :----- | :----------------------------------- |
    | `Total`     | Number | The total number of threads.         |
    | `Suspended` | Number | The number of suspended threads.     |

## 7.7. The `SubscriptionStatus` Object

Used in the `Events` array of a `Subscribed` response.

* **Structure:**

    | Name    | Type   | Description                                                           |
    | :------ | :----- | :-------------------------------------------------------------------- |
    | `ID`    | Number | The numeric identifier of the event. See Section 8.2.                 |
    | `Name`  | String | The string identifier of the event. See Section 8.2.                  |
    | `Value` | Number | `1` if the client is subscribed to this event, `0` otherwise.         |

### 7.8. The `EventInfo` Object

Used in the `Event` field of a `Notification` message.

* **Structure:**

    | Name | Type   | Description                                            |
    | :--- | :----- | :----------------------------------------------------- |
    | `ID` | Number | The numeric identifier of the event. See Section 8.2.  |
    | `Name`| String | The string identifier of the event. See Section 8.2.   |

### 7.9. `LastKnownState` Nested Objects

* **`ActivityInfo` Object:**

    | Name   | Type   | Description                                                               |
    | :----- | :----- | :------------------------------------------------------------------------ |
    | `Code` | Number | A code representing the activity. See Section 8.4.                        |
    | `TS`   | String | The UTC timestamp (ISO 8601) when this activity began.                    |

* **`LocationInfo` Object:**

    | Name       | Type   | Description                                                                 |
    | :--------- | :----- | :-------------------------------------------------------------------------- |
    | `Function` | String | The name of the function being executed.                                    |
    | `Line`     | Number | The line number being executed.                                             |
    | `TS`       | String | The UTC timestamp (ISO 8601) when execution of this line began or resumed. |

* **`WsFullInfo` Object:**

    | Name | Type   | Description                                                              |
    | :--- | :----- | :----------------------------------------------------------------------- |
    | `TS` | String | The UTC timestamp (ISO 8601) when the last WSFULL event occurred.       |

## 8. Enumerated Values

### 8.1. Fact Identifiers

| ID | Name                 |
| :- | :------------------- |
| 1  | `Host`               |
| 2  | `AccountInformation` |
| 3  | `Workspace`          |
| 4  | `Threads`            |
| 5  | `SuspendedThreads`   |
| 6  | `ThreadCount`        |

### 8.2. Subscription Event Identifiers

| ID | Name                  |
| :- | :-------------------- |
| 1  | `WorkspaceCompaction` |
| 2  | `WorkspaceResize`     |
| 3  | `UntrappedSignal`     |
| 4  | `TrappedSignal`       |
| 5  | `ThreadSwitch`        |
| 6  | `All`                 |

### 8.3. Access Levels

| Level | Description                                              |
| :---- | :------------------------------------------------------- |
| 0     | Disallow connections.                                    |
| 1     | Permit connections, restricted information and permissions. |
| 2     | Permit connections, full information, restricted permissions. |
| 3     | Permit connections, full information and permissions.    |

### 8.4. Activity Codes

| Code | Meaning                                |
| :--- | :------------------------------------- |
| 1    | Anything not specifically listed below |
| 2    | Performing a workspace allocation      |
| 3    | Performing a workspace compaction      |
| 4    | Performing a workspace check           |
| 222  | Sleeping (internal testing feature)    |

## 9. Security Considerations

The HMON protocol can expose sensitive information about a running APL application, including memory usage, thread states, and potentially source code fragments within stack traces. The level of information exposed is controlled by the `AccessLevel` configuration parameter on the interpreter. Implementers and operators should ensure that the Access Level is configured appropriately for their security requirements. Connections over untrusted networks SHOULD be secured using transport-level security, such as TLS.
