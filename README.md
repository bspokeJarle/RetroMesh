# RetroMesh

RetroMesh is the reusable retro 3D engine extracted from The Omega Strain. It
contains the game-neutral rendering, geometry, projection, collision, physics,
timing, input, and audio foundations used by the game.

## Contents

- `RetroMesh.Engine.slnx`: engine solution.
- `RetroMesh.Engine/`: engine source, package metadata, license, and engine
  documentation.
- `RetroMesh.Engine.Tests/`: engine test suite.
- `RetroMesh.Rendering.Direct3D11/`: the Windows Direct3D 11 renderer.
- `build/Build-RetroMeshEnginePackage.ps1`: local package build script.
- `artifacts/packages/`: generated local NuGet packages.

## Fresh checkout

Requirements:

- Windows x64 when building the complete solution and Direct3D 11 renderer
- [Git for Windows](https://git-scm.com/download/win)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PowerShell

From this folder:

```powershell
dotnet restore .\RetroMesh.Engine.slnx
dotnet build .\RetroMesh.Engine.slnx --no-restore
dotnet test .\RetroMesh.Engine.slnx --no-restore
```

To build a local NuGet package after the solution passes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Build-RetroMeshEnginePackage.ps1 -NoRestore
```

## The Omega Strain workshop

For the easiest complete setup, clone
[`TheOmegaStrain`](https://github.com/bspokeJarle/TheOmegaStrain) and follow its
Workshop quick start. Its setup script clones RetroMesh automatically as a
sibling repository, restores both repositories, builds the engine, runs the
engine tests, and builds the game.

The Omega Strain currently consumes the locally built `RetroMesh.Engine` and
`RetroMesh.Rendering.Direct3D11` DLLs through `RetroMeshRoot` in its
`Directory.Build.props`. The default path is `..\RetroMesh\`, so keeping the
repositories next to each other requires no machine-specific configuration.

New games should start from the separate `RetroMesh.GameTemplate` repository.
