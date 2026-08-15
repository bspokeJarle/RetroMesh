# RetroMesh

RetroMesh is the reusable engine extracted from The Omega Strain. This folder is
laid out as a standalone engine root so it can be moved to its own repository
without bringing the game along.

## Contents

- `RetroMesh.Engine.slnx`: engine solution.
- `RetroMesh.Engine/`: engine source, package metadata, license, and engine
  documentation.
- `RetroMesh.Engine.Tests/`: engine test suite.
- `build/Build-RetroMeshEnginePackage.ps1`: local package build script.
- `artifacts/packages/`: generated local NuGet packages.

## Build

From this folder:

```powershell
dotnet build .\RetroMesh.Engine.slnx --no-restore
dotnet test .\RetroMesh.Engine.slnx --no-restore
.\build\Build-RetroMeshEnginePackage.ps1
```

The Omega Strain consumes RetroMesh through the local package source configured
in the repository root `NuGet.config`.
