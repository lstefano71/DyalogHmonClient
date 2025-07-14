# Tech Context: Dyalog.Hmon.Client

## Language and Framework
- **Language:** C# 13
- **Framework:** .NET 9.0

## Core Dependencies
- System.Net.Sockets
- System.IO.Pipelines
- System.Text.Json (with Source Generation)
- System.Threading.Channels

## Technical Constraints
- Must be highly efficient and scalable for many concurrent connections.
- Use modern .NET techniques for performance and reliability.
- API must be intuitive and provide comprehensive XML documentation.
- Design should support unit and integration testing, with interface-based abstractions where appropriate.

## Tooling Preferences
- Preferred logging: Serilog
- Console/CLI interaction: SpectreConsole
- Embedded database: SQLite
