
# Copilot Instructions for Dyalog.Hmon.Client

## Project Overview
- This project is primarily developed in C# 13 targeting .NET 9.0.
- The main Product Requirements Document (PRD) is `docs/hmonclient-prd.md`. **Start here for the definitive scope, goals, and API for the library.**
- The codebase is structured for both architectural clarity and implementation efficiency. Simplicity is preferred, but complexity is embraced when necessary.
- Key protocols and architecture decisions are documented in the `RFCs/` directory. Review these for understanding service boundaries and data flows.

## Developer Workflows
- Use idiomatic, modern C# and leverage the latest language features.
- The preferred logging library is **Serilog**.
- For console/CLI interactions, use **SpectreConsole**.
- Use **SQLite** for embedded database needs.
- When unsure about a library or API, consult official documentation or context7—do not invent APIs or NuGet packages.

## Project Conventions
- Avoid unnecessary abstractions; only introduce them when they improve extensibility or clarity.
- Refactor older code to adopt new, proven patterns as they emerge.
- Do not use code comments for communication—explain rationale in markdown or documentation instead.
- Keep a current TODO list using the best available tools.


## Key Files & Directories
- `docs/hmonclient-prd.md`: **Main Product Requirements Document (PRD)** — defines the scope, goals, and API for the first phase of this project. Start here for a comprehensive understanding of what the library must deliver.
- `RFCs/`: Protocol and architecture documentation (review for service boundaries and protocol details).
- `.clinerules/Instructions.md`: Project-specific AI/LLM conventions and preferences.
- `docs/`: Additional documentation and environment-specific guides.

## Examples
- For logging, use Serilog patterns (see existing usages if present).
- For CLI, follow SpectreConsole idioms.
- For embedded storage, use SQLite with idiomatic C# wrappers.

## Integration & External Dependencies
- Review RFCs for integration points and protocol details.
- Prefer official or widely adopted libraries; avoid obscure dependencies.

---

_If any section is unclear or incomplete, please provide feedback for further refinement._
