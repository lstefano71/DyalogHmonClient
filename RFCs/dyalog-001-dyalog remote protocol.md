# RFC 0001: Dyalog Remote Protocol - Transport (DRP-T)

**Category:** Informational  
**Author:** Gemini  
**Version:** 1.0  
**Created:** July 14, 2025

## Abstract

This document specifies the Dyalog Remote Protocol - Transport (DRP-T), a session-oriented protocol that provides a common message framing and handshake mechanism for application-layer protocols such as the Remote IDE (RIDE) protocol and the Health Monitor (HMON) protocol. It operates over a reliable, stream-based transport, such as TCP.

## 1. Introduction

The Dyalog Remote Protocol is designed as a layered architecture. DRP-T defines the foundational transport layer (Layer 1) responsible for connection establishment, message framing, and an initial protocol handshake. Application-layer protocols (Layer 2) such as RIDE and HMON operate on top of DRP-T, defining their own specific message sets.

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in RFC 2119.

For the purpose of this document:

* The **"Interpreter"** refers to the Dyalog APL process that acts as the logical server, providing data and services.
* The **"Client"** refers to the external application (e.g., an IDE, a monitoring dashboard) consuming these services.

## 2. Connection Establishment

A connection is established using a standard, stream-oriented, full-duplex transport, such as a TCP/IP socket. The initiation of the connection can be performed by either the Client (e.g., connecting to an interpreter in `SERVE` mode) or the Interpreter (e.g., connecting to a client from `POLL` mode). DRP-T is agnostic to the direction of connection initiation.

## 3. Message Framing

All data transmitted over a DRP-T connection after the initial handshake MUST be sent in frames. A single DRP-T frame has the following structure:

```text
    0                   1                   2                   3
    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |                        Total Length                           |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |                       Magic Number (4 octets)                 |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |                        Payload (variable length)              |
   |                              ...                              |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

* **Total Length:** A 32-bit (4-octet) unsigned integer in big-endian format. It specifies the total length of the frame in octets, including the Total Length field itself. The value is therefore 8 plus the byte length of the Payload.

* **Magic Number:** A 4-octet field containing a sequence of ASCII characters that identifies the application-layer protocol.
  * For the RIDE protocol, this field MUST be `0x52 0x49 0x44 0x45` ("RIDE").
  * For the HMON protocol, this field MUST be `0x48 0x4D 0x4F 0x4E` ("HMON").

* **Payload:** A variable-length sequence of octets. The payload's content and encoding are defined by the specific application-layer protocol (RIDE or HMON). Typically, this is a UTF-8 encoded JSON string.

## **4. Handshake Protocol**

Immediately upon establishing the underlying transport connection, both peers MUST engage in a handshake sequence to agree on the protocol version.

The handshake consists of two messages sent by each peer. **All handshake messages MUST be framed** according to the DRP-T framing specification in Section 3. The distinction of the handshake messages is that their `Payload` field contains a raw UTF-8 encoded string, not a JSON-encoded array.

The sequence of the `Payload` content is fixed and MUST proceed as follows:

1. Client sends a DRP-T frame whose payload is the string `SupportedProtocols=2`.
2. Interpreter sends a DRP-T frame whose payload is the string `SupportedProtocols=2`.
3. Client sends a DRP-T frame whose payload is the string `UsingProtocol=2`.
4. Interpreter sends a DRP-T frame whose payload is the string `UsingProtocol=2`.

The `Magic Number` in the frame header MUST correspond to the application-layer protocol being used (e.g., "RIDE" or "HMON").

### 4.1. Example Handshake Frame

Consider the first message sent by a RIDE client: `SupportedProtocols=2`.

* The `Payload` is the UTF-8 string `SupportedProtocols=2`, which has a length of 20 octets.
* The `Total Length` is 8 (header) + 20 (payload) = 28 octets.
* The `Magic Number` is "RIDE" (`0x52 0x49 0x44 0x45`).

The resulting frame on the wire would be:

```text
    0                   1                   2                   3
    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   | 0x00 | 0x00 | 0x00 | 0x1C |  Total Length = 28 (0x1C)         |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  'R' |  'I' |  'D' |  'E' |  Magic Number = "RIDE"            |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  'S' |  'u' |  'p' |  'p' |                                   |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  'o' |  'r' |  't' |  'e' |                                   |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  'd' |  'P' |  'r' |  'o' |  Payload = "SupportedProtocols=2" |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  't' |  'o' |  'c' |  'o' |                                   |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |  'l' |  's' |  '=' |  '2' |                                   |
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

Both peers MUST send and receive these four framed messages in this exact order before any JSON-based application-layer data is transmitted.

## 5. Post-Handshake Communication

After the handshake is successfully completed, all subsequent communication MUST use the message framing described in Section 3. The payload of these frames is defined by the application-layer protocol (HMON or RIDE) identified by the Magic Number. For both RIDE and HMON, all post-handshake payloads are UTF-8 encoded JSON arrays of the form:

```json
["CommandName",{"key1":"value1","key2":222}]
```

**Boolean Serialization:**  
All boolean properties within JSON payloads MUST be serialized as integers and deserialized from integers: `0` for `false` and `1` for `true`. This ensures consistent interpretation of boolean values across different implementations and platforms.
