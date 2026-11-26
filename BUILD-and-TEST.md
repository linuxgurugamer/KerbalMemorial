# Kerbal Memorial Build and Test Instructions

## Build

Requirements: Visual Studio with the .NET Framework 4.8 targeting pack (or a compatible MSBuild installation and .NET SDK), KSP 1.12.x, ToolbarController and ClickThroughBlocker (and their normal dependencies). No ModuleManager is required by this plugin.

Set the `KSP_ROOT` environment variable to the KSP installation before launching Visual Studio, then open `KerbalMemorial.csproj` and build Release. Alternatively run:

```powershell
.\build.ps1 -KspRoot 'D:\Games\KSP'
```

The project expects `KSP_x64_Data/Managed` and the usual CKAN dependency paths. Override `KSPRoot`, `ToolbarDll` or `ClickThroughDll` with MSBuild properties if necessary. The build stages the resulting DLL into `GameData/KerbalMemorial/Plugins`. Copy the staged `GameData/KerbalMemorial` folder into your game's GameData. The source archive does not include a prebuilt DLL or dependency binaries.


## Tests

`dotnet run --project Tests/LedgerTests.csproj` on a Windows .NET 4.8 development environment tests the pure ledger. The tests and the plugin require compilation locally; the distributed source was not compiled or run in KSP. See `Tests/ManualTests.md` for in-game verification.
