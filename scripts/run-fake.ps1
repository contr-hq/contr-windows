#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Runs the built app against `contrd --backend fake` on a private pipe.

.DESCRIPTION
  The fake daemon listens on its own pipe and writes no discovery file, so a daemon already
  serving this user's default pipe is left alone. Build contrd in the core repository with
  `cargo build -p contr-daemon --features fake`, and the app with
  `dotnet build src/App/Contr.App/Contr.App.csproj -p:Platform=x64`.

.EXAMPLE
  pwsh scripts/run-fake.ps1 -Contrd C:\src\contr\target\debug\contrd.exe
#>
param(
  [Parameter(Mandatory)]
  [string]$Contrd,

  [string]$Pipe = 'contr-windows-fake',

  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root "src/App/Contr.App/bin/x64/$Configuration/net10.0-windows10.0.19041.0/Contr.App.exe"
$endpoint = "npipe:////./pipe/$Pipe"

# Enumerating \\.\pipe\ lists pipes without connecting to them.
$running = [System.IO.Directory]::GetFiles('\\.\pipe\') -contains "\\.\pipe\$Pipe"
if (-not $running) {
  $log = Join-Path ([System.IO.Path]::GetTempPath()) "$Pipe.log"
  Start-Process -FilePath $Contrd -ArgumentList '--backend', 'fake', '--listen', $endpoint `
    -WindowStyle Hidden -RedirectStandardError $log
  Write-Output "contrd --backend fake listens on $endpoint; log: $log"
}

$env:CONTR_HOST = $endpoint
Start-Process -FilePath $app
