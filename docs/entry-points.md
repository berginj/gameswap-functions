# Entry points audit

## Azure Functions
- Function endpoints live under `Functions/` and use the isolated worker model.
- The function host startup is defined in `Program.cs`.

## .NET host
- The host is configured in `Program.cs` with dependency registration and Azure Functions worker setup.

## Front-end roots
- Primary UI assets live in `fsa-www/`.
- Legacy/alternate UI assets live in `Turf/` (verify active usage before changes).
