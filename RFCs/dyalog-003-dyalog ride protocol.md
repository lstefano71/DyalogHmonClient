# Dyalog Remote IDE (RIDE) Protocol

**Category:** Informational  
**Author:** Gemini  
**Version:** 1.0  
**Created:** July 14, 2025

## Abstract

This document specifies the Dyalog Remote IDE (RIDE) protocol, an application-layer protocol for remotely controlling a Dyalog APL interpreter as a full-featured interactive development environment. The RIDE protocol enables a client application to execute code, edit and manage workspace objects, and debug running applications. It operates over the Dyalog Remote Protocol - Transport (DRP-T), as defined in RFC 0001.

## Status of This Memo

This document provides information for the Internet community. It does not specify an Internet standard of any kind. Distribution of this memo is unlimited.

## 1. Introduction

The RIDE protocol facilitates a rich, interactive session between a development client (the IDE) and a Dyalog APL interpreter. It provides the necessary commands and data structures for a client to build a complete user interface for APL development, including a session log, an editor, a tracer, and a workspace explorer.

This specification defines the syntax and semantics of all RIDE messages. It assumes a DRP-T session has been successfully established between the client and the interpreter.

### 1.1. Terminology

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in RFC 2119.

* **Client:** The IDE application.
* **Interpreter:** The Dyalog APL process.

## 2. Transport

The RIDE protocol operates exclusively over a Dyalog Remote Protocol - Transport (DRP-T) session, as specified in RFC 0001.

* **Magic Number:** The Magic Number field in all RIDE DRP-T frames MUST be the 4-octet ASCII sequence "RIDE" (`0x52 0x49 0x44 0x45`).

## 3. Message Format

All RIDE messages are transported as the payload of a DRP-T frame. The payload MUST be a UTF-8 encoded JSON array containing exactly two elements:

1.A `MessageName` (String) that identifies the command or response.
2.An `Arguments` (Object) that contains key-value pairs specific to the message.

Example: `["CommandName", {"arg1": "value1", "arg2": 123}]`

All message and argument names are case-sensitive.

## 4. Connection and Session Management

### 4.1. Identify

Sent by the Client to the Interpreter immediately after the DRP-T handshake to declare its identity and desired API version.

* **Example:** `["Identify", {"apiVersion": 1, "identity": 1}]`
* **Arguments:**

    | Name         | Type   | Required | Description                                                    |
    | :----------- | :----- | :------- | :------------------------------------------------------------- |
    | `apiVersion` | Number | Yes      | The API version the client wishes to use. Currently `1`.        |
    | `identity`   | Number | Yes      | A code identifying the application type. For RIDE, this is `1`. |

### 4.2. ReplyIdentify

The Interpreter's response to `Identify`, providing details about itself.

* **Example:** `["ReplyIdentify", {"apiVersion": 1, "version": "18.2.46943", "platform": "Windows-64", "arch": "Unicode/64", "Project": "CLEAR WS", "pid": 1000}]`
* **Arguments:**

    | Name        | Type   | Description                                            |
    | :---------- | :----- | :----------------------------------------------------- |
    | `apiVersion`| Number | The negotiated API version accepted by the interpreter.|
    | `version`   | String | The interpreter version string (e.g., "18.2.46943").     |
    | `platform`  | String | The operating system and architecture (e.g., "Windows-64").|
    | `arch`      | String | The interpreter edition (e.g., "Unicode/64").          |
    | `Project`   | String | The name of the current project or workspace.          |
    | `pid`       | Number | The interpreter's process ID.                          |
    | `...`       | ...    | Other implementation-specific details MAY be present.  |

### 4.3. GetLog

Sent by the Client to request the session log from the Interpreter.

* **Example:** `["GetLog", {"format": "json", "maxLines": 100}]`
* **Arguments:**

    | Name       | Type   | Required | Description                                                    |
    | :--------- | :----- | :------- | :------------------------------------------------------------- |
    | `format`   | String | No       | `json` or `text`. If omitted, defaults to `json`.               |
    | `maxLines` | Number | No       | Limits the number of lines returned. `-1` means unlimited.    |

### 4.4. ReplyGetLog

The Interpreter's response containing the session log. This message MAY be sent in multiple parts to stream a large log.

* **Example (`json`):** `["ReplyGetLog", {"result": [{"group": 1, "type": 1, "text": "line 1"}]}]`
* **Arguments:**

    | Name     | Type  | Description                                                                                                                                                                                                                                                                          |
    | :------- | :---- | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
    | `result` | Array | If `format` was "text", an array of strings. If `format` was "json", an array of objects, where each object has `group` (Number, for grouping related output), `type` (Number, see Section 4.6), and `text` (String). |

### 4.5. Execute

Sent by the Client to the Interpreter to execute a line of APL code.

* **Example:** `["Execute", {"text": "1 2 3 + 4 5 6", "trace": 0}]`
* **Arguments:**

    | Name    | Type   | Required | Description                                                                                             |
    | :------ | :----- | :------- | :------------------------------------------------------------------------------------------------------ |
    | `text`  | String | Yes      | The APL code to evaluate. MUST end with a newline (`\n`) character.                                     |
    | `trace` | Number | No       | A code indicating execution mode: `0`=Execute, `1`=Trace Line (`<TC>`), `2`=Trace Token (`<IT>`). Default is `0`. |

### 4.6. AppendSessionOutput

Sent by the Interpreter to the Client to provide text output for the session log.

* **Example:** `["AppendSessionOutput", {"result": "5 7 9", "type": 1, "group": 1}]`
* **Arguments:**

    | Name     | Type   | Required | Description                                                                                                                             |
    | :------- | :----- | :------- | :-------------------------------------------------------------------------------------------------------------------------------------- |
    | `result` | String | Yes      | The output text.                                                                                                                        |
    | `type`   | Number | Yes      | A code indicating the source of the output. See Section 10.1 for a list of type codes.                                                 |
    | `group`  | Number | Yes      | A number used to visually group related lines of output (e.g., a multi-line array display). `0` indicates no specific grouping.         |

### 4.7. SetPromptType

Sent by the Interpreter to the Client to indicate its input readiness state.

* **Example:** `["SetPromptType", {"type": 1}]`
* **Arguments:**

    | Name   | Type   | Description                                                                          |
    | :----- | :----- | :----------------------------------------------------------------------------------- |
    | `type` | Number | A code indicating the prompt type. See Section 10.2 for a list of type codes. |

### 4.8. HadError

Sent by the Interpreter to the Client to signal that an error occurred during the execution of a user submission. The client SHOULD clear any pending execution queue upon receipt.

* **Example:** `["HadError", {}]`
* **Arguments:** The arguments object MUST be empty.

### 4.9. Disconnect

Sent by either peer to signal an orderly shutdown of the connection.

* **Example:** `["Disconnect", {"message": "User shutdown request"}]`
* **Arguments:**

    | Name      | Type   | Description                                           |
    | :-------- | :----- | :---------------------------------------------------- |
    | `message` | String | A human-readable string explaining the disconnection. |

## 5. Editor and Tracer Management

### 5.1. Edit

Sent by the Client to the Interpreter to request opening an editor for an object.

* **Example:** `["Edit", {"win": 0, "text": "myfn", "pos": 4, "unsaved": {"124": "new content"}}]`
* **Arguments:**

    | Name      | Type   | Required | Description                                                                                        |
    | :-------- | :----- | :------- | :------------------------------------------------------------------------------------------------- |
    | `win`     | Number | Yes      | The ID of the window making the request (e.g., `0` for the session).                                 |
    | `text`    | String | Yes      | The text expression identifying the object to edit (e.g., a name or `⎕ED` argument).                  |
    | `pos`     | Number | Yes      | The 0-based character index of the cursor within `text`.                                           |
    | `unsaved` | Object | No       | A map of `window_id` (String) to `content` (String), representing any unsaved changes in other windows. |

### 5.2. OpenWindow

Sent by the Interpreter to instruct the Client to open a new editor or tracer window.

* **Example:** `["OpenWindow", {"name": "f", "text": ["r←f a"], "token": 123, "entityType": 1, ...}]`
* **Arguments:**

    | Name           | Type             | Description                                                                                                     |
    | :------------- | :--------------- | :-------------------------------------------------------------------------------------------------------------- |
    | `name`         | String           | The name of the object being edited.                                                                            |
    | `filename`     | String           | The path to the source file, if the object is linked to one.                                                    |
    | `text`         | Array of Strings | The content of the object, as an array of lines.                                                                |
    | `token`        | Number           | A unique identifier for this window. All subsequent messages related to this window MUST use this token.        |
    | `currentRow`   | Number           | The initial 0-based line number for the cursor.                                                                 |
    | `debugger`     | Boolean          | `true` if this window is a tracer, `false` for an editor.                                                       |
    | `entityType`   | Number           | A code classifying the object type. See Section 10.3.                                                           |
    | `readOnly`     | Boolean          | `true` if the content is not editable.                                                                          |
    | `stop`         | Array of Numbers | An array of 0-based line numbers where breakpoints (`⎕STOP`) are set.                                             |
    | `trace`        | Array of Numbers | An array of 0-based line numbers where tracepoints (`⎕TRACE`) are set.                                            |
    | `monitor`      | Array of Numbers | An array of 0-based line numbers where monitor points (`⎕MONITOR`) are set.                                       |
    | `tid`          | Number           | The thread ID this window is associated with.                                                                   |
    | `tname`        | String           | The name of the thread.                                                                                         |

### 5.3. UpdateWindow

Sent by the Interpreter to update the content or state of an existing window. The arguments are identical to `OpenWindow`.

### 5.4. CloseWindow

Sent by either the Client or Interpreter to signal the closing of a window. The peer that receives the message MUST respond with a `CloseWindow` message for the same window ID to confirm closure.

* **Example:** `["CloseWindow", {"win": 123}]`
* **Arguments:**

    | Name  | Type   | Description                    |
    | :---- | :----- | :----------------------------- |
    | `win` | Number | The ID of the window to close. |

### 5.5. SaveChanges

Sent by the Client to the Interpreter to submit the contents of an editor to be fixed in the workspace.

* **Example:** `["SaveChanges", {"win": 123, "text": ["r←avg a", "r←(+/a)÷≢a"], "stop": [2]}]`
* **Arguments:**

    | Name    | Type             | Description                                                                                           |
    | :------ | :--------------- | :---------------------------------------------------------------------------------------------------- |
    | `win`   | Number           | The ID of the window being saved.                                                                     |
    | `text`  | Array of Strings | The new content, as an array of lines.                                                                |
    | `stop`  | Array of Numbers | The new set of 0-based line numbers where breakpoints (`⎕STOP`) should be set for this object.          |
    | `trace` | Array of Numbers | The new set of 0-based line numbers where tracepoints (`⎕TRACE`) should be set for this object.         |
    | `monitor`| Array of Numbers | The new set of 0-based line numbers where monitor points (`⎕MONITOR`) should be set for this object. |

### 5.6. ReplySaveChanges

The Interpreter's response to `SaveChanges`.

* **Example:** `["ReplySaveChanges", {"win": 123, "err": 0}]`
* **Arguments:**

    | Name  | Type   | Description                                   |
    | :---- | :----- | :-------------------------------------------- |
    | `win` | Number | The ID of the window that was being saved.    |
    | `err` | Number | `0` on success, a non-zero error code on failure. |

### 5.7. SetHighlightLine

Sent by the Interpreter to the Client to indicate the currently executing line in a tracer window.

* **Example:** `["SetHighlightLine", {"win": 123, "line": 1, "end_line": 1, "start_col": 0, "end_col": 5}]`
* **Arguments:**

    | Name        | Type   | Description                                                                       |
    | :---------- | :----- | :-------------------------------------------------------------------------------- |
    | `win`       | Number | The ID of the tracer window.                                                      |
    | `line`      | Number | The 0-based line number to highlight.                                             |
    | `end_line`  | Number | The 0-based end line number for multi-line highlighting. OPTIONAL.                |
    | `start_col` | Number | The 0-based start column index for token-level highlighting. `-1` indicates whole line. |
    | `end_col`   | Number | The 0-based end column index for token-level highlighting. `-1` indicates whole line.   |

## 6. Debugging Commands

The following messages are sent from the Client to the Interpreter to control a debugging session.

| MessageName         | Arguments                 | Description                                                          |
| :------------------ | :------------------------ | :------------------------------------------------------------------- |
| `TraceBackward`     | `{"win": Number}`         | Moves the current execution line back one step in the specified tracer window. |
| `Continue`          | `{"win": Number}`         | Resumes execution of the current thread in the specified tracer window.  |
| `ContinueTrace`     | `{"win": Number}`         | Resumes execution, stopping at the next line in the calling function. |
| `Cutback`           | `{"win": Number}`         | Cuts the stack back one level.                                       |
| `TraceForward`      | `{"win": Number}`         | Moves the current execution line forward one step.                   |
| `RestartThreads`    | `{}`                      | Resumes execution of all paused or suspended threads.                |
| `RunCurrentLine`    | `{"win": Number}`         | Executes the current line ("Step Over").                             |
| `StepInto`          | `{"win": Number}`         | Executes the current line, stepping into function calls ("Step Into").|
| `TracePrimitive`    | `{"win": Number}`         | Executes the current line one primitive at a time.                   |
| `WeakInterrupt`     | `{}`                      | Sends a weak interrupt signal to the interpreter.                    |
| `StrongInterrupt`   | `{}`                      | Sends a strong interrupt signal to the interpreter.                  |

## 7. Autocompletion and Value Tips

### 7.1. GetAutocomplete

Sent by the Client to request name completion suggestions.

* **Example:** `["GetAutocomplete", {"line": "r←1+ab", "pos": 6, "token": 234}]`
* **Arguments:**

    | Name    | Type   | Description                                           |
    | :------ | :----- | :---------------------------------------------------- |
    | `line`  | String | The current line of text.                             |
    | `pos`   | Number | The 0-based character index of the cursor in `line`.  |
    | `token` | Number | A request identifier, which SHOULD be the window ID.  |

### 7.2. ReplyGetAutocomplete

The Interpreter's response to `GetAutocomplete`.

* **Example:** `["ReplyGetAutocomplete", {"skip": 2, "options": ["abc", "abde"], "token": 234}]`
* **Arguments:**

    | Name      | Type             | Description                                                                  |
    | :-------- | :--------------- | :--------------------------------------------------------------------------- |
    | `skip`    | Number           | The number of characters before `pos` that should be replaced by a completion. |
    | `options` | Array of Strings | A list of completion suggestions.                                            |
    | `token`   | Number           | The token from the original request.                                         |

### 7.3. GetValueTip

Sent by the Client to request a value tip for a name under the cursor.

* **Example:** `["GetValueTip", {"win": 123, "line": "a←b+c", "pos": 2, "maxWidth": 50, "maxHeight": 20, "token": 456}]`
* **Arguments:**

    | Name        | Type   | Description                                                            |
    | :---------- | :----- | :--------------------------------------------------------------------- |
    | `win`       | Number | The ID of the window where the request originates.                     |
    | `line`      | String | The current line of text.                                              |
    | `pos`       | Number | The 0-based character index of the cursor in `line`.                   |
    | `maxWidth`  | Number | The maximum number of columns for the tip's content.                   |
    | `maxHeight` | Number | The maximum number of lines for the tip's content.                     |
    | `token`     | Number | A request identifier.                                                  |

### 7.4. ValueTip

The Interpreter's response to `GetValueTip`.

* **Example:** `["ValueTip", {"tip": ["1 2 3", "4 5 6"], "class": 4, "startCol": 2, "endCol": 3, "token": 456}]`
* **Arguments:**

    | Name       | Type             | Description                                                              |
    | :--------- | :--------------- | :----------------------------------------------------------------------- |
    | `tip`      | Array of Strings | The content of the value tip, as an array of lines.                      |
    | `class`    | Number           | The name class (`⎕NC`) of the object, for styling purposes.              |
    | `startCol` | Number           | The 0-based inclusive start column of the name the tip pertains to.      |
    | `endCol`   | Number           | The 0-based exclusive end column of the name the tip pertains to.        |
    | `token`    | Number           | The token from the original request.                                     |

## 8. Workspace Explorer

### 8.1. TreeList

Sent by the Client to request the children of a node in the workspace tree.

* **Example:** `["TreeList", {"nodeId": 12}]`
* **Arguments:**

    | Name     | Type   | Description                               |
    | :------- | :----- | :---------------------------------------- |
    | `nodeId` | Number | The ID of the parent node. Root is `0`.   |

### 8.2. ReplyTreeList

The Interpreter's response to `TreeList`.

* **Example:** `["ReplyTreeList", {"nodeId": 12, "nodeIds": [34, 0], "names": ["ab", "cde"], "classes": [9.4, 3.2], "err": ""}]`
* **Arguments:**

    | Name      | Type             | Description                                                                          |
    | :-------- | :--------------- | :----------------------------------------------------------------------------------- |
    | `nodeId`  | Number           | The ID of the parent node from the request.                                          |
    | `nodeIds` | Array of Numbers | An array of IDs for the child nodes. A `0` indicates a leaf node.                      |
    | `names`   | Array of Strings | An array of names for the child nodes.                                               |
    | `classes` | Array of Numbers | An array of `⎕NC` values for the child nodes, used for styling.                        |
    | `err`     | String           | A non-empty string if an error occurred (e.g., `nodeId` is no longer valid).          |

## 9. Miscellaneous

### 9.1. ShowHTML

Sent by the Interpreter (via `3500⌶`) to request the Client display an HTML document.

* **Example:** `["ShowHTML", {"title": "Example", "html": "<i>Hello</i> <b>world</b>"}]`
* **Arguments:**

    | Name    | Type   | Description                    |
    | :------ | :----- | :----------------------------- |
    | `title` | String | The title for the HTML window. |
    | `html`  | String | The HTML content to display.   |

### 9.2. UpdateDisplayName

Sent by the Interpreter when the workspace ID (`⎕WSID`) changes.

* **Example:** `["UpdateDisplayName", {"displayName": "CLEAR WS"}]`
* **Arguments:** `displayName` (String) is the new workspace name.

## 10. Enumerated Values

### 10.1. Session Output `type` Codes

| Code | Description                |
| :--- | :------------------------- |
| 1    | Undetermined session output|
| 2    | Default output             |
| 3    | Stderr output              |
| 4    | System command output      |
| 5    | APL error message          |
| 7    | Quad (`⎕`) output          |
| 8    | Quote-Quad (`⍞`) output    |
| 9    | Status window information  |
| 11   | Echoed input               |
| 12   | Trace (`⎕TRACE`) output    |
| 14   | Normal input line          |

### 10.2. Prompt `type` Codes

| Code | Description                  |
| :--- | :--------------------------- |
| 0    | No prompt (busy)             |
| 1    | Standard APL prompt (6 spaces) |
| 2    | Quad (`⎕`) input prompt       |
| 3    | Line editor prompt           |
| 4    | Quote-Quad (`⍞`) input prompt |
| 5    | Other/unforeseen prompt      |

### 10.3. `entityType` Codes

| Code   | Description              |
| :----- | :----------------------- |
| 1      | Defined Function         |
| 2      | Simple Character Array   |
| 4      | Simple Numeric Array     |
| 8      | Mixed Simple Array       |
| 16     | Nested Array             |
| 32     | `⎕OR` Object             |
| 64     | Native File              |
| 128    | Simple Character Vector  |
| 256    | APL Namespace            |
| 512    | APL Class                |
| 1024   | APL Interface            |
| 4096   | External Function        |
| 262144 | Array Notation (APLAN)   |

## 11. Security Considerations

The RIDE protocol provides powerful, direct access to an APL interpreter's environment, including the ability to execute arbitrary code. Connections SHOULD only be established between trusted clients and interpreters. The use of transport-level security, such as TLS, is strongly RECOMMENDED for all RIDE connections over untrusted networks.
