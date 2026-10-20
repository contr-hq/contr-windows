# Contr for Windows: working rules

- Clean room: never inspect, analyze or read files from Docker Desktop, never use the rea MCP server, never read the
  `contr-research` repository. Behavior comes from the core repository's `specs/`.
- Comments follow [docs/COMMENTS.md](docs/COMMENTS.md): facts the code cannot show; `///` on declarations; `//` only
  in bodies, at most two lines.
- The daemon is reached only through the SDK in `gen/Contr.Sdk`; only `*.UI` projects reference it, and the host sees
  the connection as `IDaemonConnection`. `gen/` is generated: run `scripts/update-sdk.sh`, never edit it.
- Follow kakehashi conventions: `Directory.Build.props` (Nullable, TreatWarningsAsErrors), `.editorconfig`, analyzers,
  `dotnet format --verify-no-changes`, `Result<T>` for expected failures, pages load from `Loaded`.
- Layout and module recipe: [docs/architecture.md](docs/architecture.md); style: [docs/csharp-style.md](docs/csharp-style.md).
- Checks before every push, on Windows:

  ```pwsh
  dotnet format Contr.slnx --verify-no-changes --severity warn --exclude gen
  dotnet build-server shutdown
  dotnet build Contr.slnx -c Release
  dotnet test Contr.slnx -c Release --no-build
  ```

- Conventional Commits; `main` is protected.
