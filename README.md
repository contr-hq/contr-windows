# Contr for Windows

WinUI 3 desktop client for [Contr](https://github.com/contr-hq/contr). It talks to the `contrd` daemon only through the
generated `Contr.Sdk` NuGet package, over a per-user named pipe.

## Status

Not started. Scaffolding begins in core Phase 3.

## Planned stack

- .NET 10, Windows App SDK, WinUI 3, scaffolded from [kakehashi](https://github.com/SekiroKenjii/kakehashi):
  modular monolith, MVVM with CommunityToolkit.Mvvm, Generic Host, startup orchestrators.
- Modules: Containers, Images, Volumes, Networks, Compose, Kubernetes, Troubleshoot, Settings, Onboarding.
- Tray (WinUIEx), single instance, WebView2 + xterm.js terminal for exec and logs.
- Velopack installer with an MSI for Intune and Group Policy, bundling a pinned core release.

## License

Apache-2.0. Docker is a trademark of Docker, Inc.; Contr is not affiliated with or endorsed by Docker, Inc.
