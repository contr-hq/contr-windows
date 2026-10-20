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
src/Shared/Contr.UI.Common         PageHeader, PillView, PillRow, Pager, EngineGate, EmptyState, Sparkline, styles,
                                   the icon font's glyph table, live lists and paging
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
- `IntegrationClient` serves `ICliTools` and `IWslDistributions` over `ContrClient.Integration`, on the
  same connection. Setup and Settings show the WSL distributions only when the handshake lists
  `WslIntegration`.
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
| 35 | Tray | WinUIEx `TrayIcon`: a click opens the flyout, a double click the window |
| 40 | Activation | skipped when Windows started Contr at sign-in (`--background`): only the tray shows |
| 45 | Onboarding | the first-run setup, until it records `SetupDone`; never on a `--background` launch |

Closing the main window hides it; only **Quit Contr** in the tray ends the process. The daemon keeps
running either way.

## Pages

Pages follow the design's principles: WinUI 3 decides how each control looks and behaves, kakehashi how a page
is put together. A page is a `ScrollViewer` around a 24 px padded grid whose one column is no wider than
`PageContentMaxWidth` (1240 px), and its blocks stand 16 px apart. It opens with `PageHeader`: a breadcrumb that
starts at the product and ends SemiBold at the page, with the page's actions on the right. A page has no title of
its own; a page under another names its trail, and `TrailClicked` reports a click on an ancestor.

```xml
<common:PageHeader Title="Volumes">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Style="{StaticResource AccentButtonStyle}" …/>
        <Button …/>
    </StackPanel>
</common:PageHeader>
```

One button on a page or in a dialog is accent, its primary action; `DestructiveButtonStyle` gives a destructive
one red text, and confirmations make Cancel the default. Under the header come the page's messages, before any
card: the shell's notifications in the header's own `InfoBar`, then the page's error `InfoBar`, whose title says
what failed and whose body is the daemon's message. A list page then shows a `PillRow` of `PillItem`s counted
over every item rather than the filtered ones, then its content in captioned cards: `CaptionedCardStyle`, an
upper-case `CaptionLabelStyle` caption, then rows. A list inside a card pages at five rows with `Paging<T>`,
whose caption reads such as `IMAGES · 1–5 OF 9`, and a `Pager` that shows only when there is a second page;
the Containers list stays one grouped table. Two columns are a flexible one and a fixed 400 px one, such as a
list and its details. Key–value rows use the 170 px `FieldLabelWidth`.

Status is a `PillView`: a 7 px dot and 12 px text, its `PillKind` mapped from a `StatusKind` with
`PillView.For`. Identifiers, such as image references, IDs, ports and paths, use `IdentifierTextStyle`; times
read relative with `HumanFormat.Ago`, such as `14m ago`, then a date. `Cards.xaml` holds the card, caption,
field, identifier, time and pill styles; `Lists.xaml` the card table's head and rows, with an inset variant for
rows that select a details card; `Menus.xaml` the row and toolbar buttons and the view switch; `SelectorBar.xaml`
the chips that filter a view, such as Compose's services. Settings and Kubernetes merge `Styles/SettingsGroups.xaml`,
which turns each `SettingsCard` into a row of a captioned group.

Icons come from the Contr Icons font, built from Fluent UI System Icons (MIT): `assets/icons/svg` holds one SVG
per icon and `assets/icons/icons.json` pins each one's code point. `scripts/build-icon-font.py` writes
`Assets/Fonts/ContrIcons.ttf` and the `ContrIcons` constants; a `FontIcon` with `IconStyle` draws a glyph:

```xml
<FontIcon Glyph="{x:Bind icons:ContrIcons.Containers}" Style="{StaticResource IconStyle}" />
```

Pages start loading from `Loaded`, never `OnNavigatedTo`: pages
come from the container and the navigation service sets `Frame.Content` directly. A page that takes
navigation parameters, such as a container's ID, implements `IHasViewModel`; the navigation service
hands the parameters to `ViewModel.OnNavigatedTo` before the page loads. Pages reached from a module's
own pages rather than the pane, such as the container page, come from `IModule.GetPages`.

A list page derives its view model from `LiveListViewModel<TItem, TRow>`, which follows the module's
Watch stream between `Loaded` and `Unloaded` and watches again three seconds after a failure. Rows are
immutable records; the stream replaces them in place. A page over engine objects shows `EngineGate`
instead of its content while the engine is not running, and `EmptyState` when nothing is listed; a status
always comes with a word, never a colour alone.

`x:Bind` calls small `public static` helpers on the page for formatting and visibility instead of value
converters, because a function is compile-checked against its arguments. Brushes come from
`{ThemeResource}` in XAML, or `ThemeResources.Brush` in code: `Application.Current.Resources` answers for
the theme the app started in.

A `NavigationItem` can carry a `Badge`, such as the Containers count, and an `Order` that places it among every
module's items; `Tools` items, such as Troubleshoot, sit below a separator. The shell's own Kubernetes and
Troubleshoot pages are items like the modules'.

A surface outside a module reaches the module's data through a contract in `Contr.UI.Contracts`, never the module:
the tray flyout lists running containers through `IRunningContainers`, which the Containers module implements, and
the title bar's search asks every `ISearchSource` a module registers. A hit names the page and the arguments that
show it, such as the Volumes page and a volume's name.
Compose projects are read from the labels Compose puts on containers, so the Compose page lives in the Containers
module. The status bar spans the window
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
