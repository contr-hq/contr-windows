# Architecture

Contr for Windows is a WinUI 3 modular monolith scaffolded from
[kakehashi](https://github.com/SekiroKenjii/kakehashi): one executable composed of feature modules,
each split into three layers. It talks to the `contrd` daemon only through the generated C# SDK, over
the per-user named pipe.

## Projects

```text
gen/Contr.Sdk                      the C# SDK contr-sdkgen generates from the core repository's proto/
src/App/Contr.App                  the host: startup orchestrators, shell, tray, settings, platform services
src/App/Contr.App.Infrastructure   OpenTelemetry wiring
src/Shared/Contr.SharedKernel      Result, Error, Entity, ValueObject; depends on nothing
src/Shared/Contr.Application.Abstractions
                                   mediator contracts, DaemonErrors, ListChange<T>
src/Shared/Contr.Mediator          the in-process mediator and its logging behavior
src/Shared/Contr.UI.Contracts      IModule, NavigationItem, platform services, IDaemonConnection
src/Shared/Contr.UI.Common         PageHeader, EngineGate, EmptyState, StatusLabel, Sparkline, styles, live lists
src/Shared/Contr.Daemon.UI         the one daemon connection and the SDK call helpers
src/Modules/<X>/Contr.Modules.<X>.{Domain,Application,UI}
                                   Containers, Images, Volumes, Networks
tools/Contr.Analyzers              the layout rules KH0001-KH0007
```

## The three layers of a module

| Layer | Holds | May reference |
| --- | --- | --- |
| Domain | IDs and value objects (`ContainerId`, `ImageReference`, `VolumeName`), state rules (`ContainerLifecycle`), errors | SharedKernel |
| Application | the gateway port, DTOs, one command or query per use case with its handler | Domain, Application.Abstractions, Mediator |
| UI | the SDK gateway, view models, pages, `IModule` registration | Application, UI.Common, UI.Contracts, Daemon.UI, the SDK |

View models reach the daemon only through the mediator (`ISender`). Gateways return
`Task<Result>` for calls and `IAsyncEnumerable<T>` for streams; a stream never throws for a daemon
failure but ends with a failed item (`ListChange<T>.Failed`, `PullEvent.Failed`).

## The daemon boundary

Only `*.UI` projects reference `gen/Contr.Sdk`. `Contr.ArchitectureTests` fails when another project
references it or names its namespace, and `Contr.App.Tests` checks the compiled host.

`Contr.Daemon.UI` owns the application's single connection:

- `DaemonConnection` connects, starts `contrd.exe` when nothing answers at the per-user endpoint,
  performs the handshake, follows `EngineService.WatchState`, starts an engine it finds stopped
  (`Daemon:StartEngine`), and reconnects every five seconds after the connection breaks. Each
  connection attempt is bounded to two seconds, because the SDK's named-pipe connect waits forever
  for a pipe that does not exist.
- `DaemonCalls.CallAsync`, `RunAsync` and `StreamAsync` turn SDK and gRPC failures into
  `DaemonErrors`, so module gateways stay a few lines each.
- The host sees the connection only as `IDaemonConnection` (in `Contr.UI.Contracts`, SDK-free).

The daemon is found through `CONTR_HOST`, then the daemon's discovery file, then the per-user
default pipe. `Daemon:Path` and `Daemon:Arguments` in `appsettings.json` choose the executable the
app starts; by default it runs the `contrd.exe` beside the app, else the one on `PATH`.

## Startup

`Program.Main` claims the single-instance key `contr-<user SID>` with
`AppInstance.FindOrRegisterForKey`; a second launch redirects its activation to the running
instance, which brings its window back. The host then runs the startup orchestrators in order:

| Order | Step | |
| --- | --- | --- |
| 5 | Accent | before any window resolves an accent brush |
| 10 | Splash | |
| 15 | DaemonConnection | connect or start, then handshake; waits at most five seconds and never fails startup |
| 20 | Shell | main window, navigation pane, status bar |
| 30 | Theme | |
| 35 | Tray | WinUIEx `TrayIcon`: left click opens the window, the menu opens or quits |
| 40 | Activation | |

Closing the main window hides it; only **Quit Contr** in the tray ends the process. The daemon keeps
running either way.

## Pages

Every page opens with `PageHeader`: a title or a breadcrumb trail, an optional subtitle, and the page's
commands as its content on the right. Pages start loading from `Loaded`, never `OnNavigatedTo`: pages
come from the container and the navigation service sets `Frame.Content` directly. A page that takes
navigation parameters, such as a container's ID, implements `IHasViewModel`; the navigation service
hands the parameters to `ViewModel.OnNavigatedTo` before the page loads. Pages reached from a module's
own pages rather than the pane, such as the container page, come from `IModule.GetPages`.

A list page derives its view model from `LiveListViewModel<TItem, TRow>`, which follows the module's
Watch stream between `Loaded` and `Unloaded` and watches again three seconds after a failure. Rows are
immutable records; the stream replaces them in place. A page over engine objects shows `EngineGate`
instead of its content while the engine is not running, `EmptyState` when nothing is listed, and
`StatusLabel` for every state, so an icon always comes with a word.

`x:Bind` calls small `public static` helpers on the page for formatting and visibility instead of value
converters, because a function is compile-checked against its arguments. Brushes come from
`{ThemeResource}` in XAML, or `ThemeResources.Brush` in code: `Application.Current.Resources` answers for
the theme the app started in.

A `NavigationItem` can carry a `Badge`, such as the Containers count. The status bar spans the window
under the pane: the engine state (its menu starts, stops and restarts the engine), when it stopped, the
backend, the engine version and the Docker context.

## Tests

| Project | Covers |
| --- | --- |
| `Contr.Modules.<X>.Domain.Tests` | IDs, reference and name validation, lifecycle rules |
| `Contr.Modules.<X>.Application.Tests` | handlers against a substituted gateway |
| `Contr.Modules.<X>.UI.Tests` | view models with a substituted `ISender`, SDK mapping, log decoding |
| `Contr.Daemon.UI.Tests` | error mapping, call and stream helpers, the connection's lifecycle |
| `Contr.App.Tests` | orchestrators, shell and settings view models, live lists, formatting, the host's references |
| `Contr.ArchitectureTests` | layering and the SDK boundary |

The `*.UI.Tests` and `Contr.App.Tests` projects are unpackaged WinUI executables and need the
Windows App Runtime installed. No test constructs XAML.

## Running against the fake daemon

`scripts/run-fake.ps1` starts `contrd --backend fake` on a private pipe and launches the app with
`CONTR_HOST` pointing at it, leaving any daemon on the default pipe alone. The fake backend serves the
fixture containers, images, volumes and networks; it does not implement logs or image pulls, so those
show the daemon's error.

## Adding a module

1. Create `src/Modules/<X>/Contr.Modules.<X>.{Domain,Application,UI}` and copy the project files of
   an existing module; only the UI project references `gen/Contr.Sdk`.
2. Declare the gateway port in Application and implement it in `UI/Infrastructure` with
   `IDaemonClient` and `DaemonCalls`.
3. Implement `IModule` in the UI project and add it to `src/App/Contr.App/Composition/ModuleCatalog.cs`.
4. Add the Domain, Application and UI test projects, the module's two assemblies to
   `LayeringTests.Layers`, and every project to `Contr.slnx`.
