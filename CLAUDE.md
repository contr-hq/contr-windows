# Contr for Windows: working rules

- Clean room: never inspect, analyze or read files from Docker Desktop, never use the rea MCP server, never read the
  `contr-research` repository. Behavior comes from the core repository's `specs/`.
- Comments follow [docs/COMMENTS.md](docs/COMMENTS.md): facts the code cannot show; `///` on declarations; `//` only
  in bodies, at most two lines.
- The daemon is reached only through `Contr.Sdk`; only `*.UI` adapter projects reference it.
- Follow kakehashi conventions: `Directory.Build.props` (Nullable, TreatWarningsAsErrors), `.editorconfig`, analyzers,
  `dotnet format --verify-no-changes`, `Result<T>` for expected failures, pages load from `Loaded`.
- Conventional Commits; `main` is protected.
