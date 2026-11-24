# Kerbal Memorial

A passive casualty tracker for Kerbal Space Program 1.12.x. Open the memorial from the stock or Blizzy toolbar to review deceased, missing, returned and respawned Kerbals, inspect their last known vessel and flight circumstances, add notes, view statistics and export CSV.

## Build

Requirements: Visual Studio with the .NET Framework 4.8 targeting pack (or a compatible MSBuild installation and .NET SDK), KSP 1.12.x, ToolbarController and ClickThroughBlocker (and their normal dependencies). No ModuleManager is required by this plugin.

Set the `KSP_ROOT` environment variable to the KSP installation before launching Visual Studio, then open `KerbalMemorial.csproj` and build Release. Alternatively run:

```powershell
.\build.ps1 -KspRoot 'D:\Games\KSP'
```

The project expects `KSP_x64_Data/Managed` and the usual CKAN dependency paths. Override `KSPRoot`, `ToolbarDll` or `ClickThroughDll` with MSBuild properties if necessary. The build stages the resulting DLL into `GameData/KerbalMemorial/Plugins`. Copy the staged `GameData/KerbalMemorial` folder into your game's GameData. The source archive does not include a prebuilt DLL or dependency binaries.

## Behavior

- Drag the unused window background to move the memorial; use the corner to resize and the centered Close button at the bottom to dismiss it.
- Select **Memorial Wall** beside the main tabs to open a separate resizable, draggable wall. The original memorial artwork supplies the decorative heading, side panel and inscription; the fictional plaques are replaced with dynamic plaques containing the actual saved portraits, names, dates and loss status. Scroll to see every recorded fatality (including respawned Kerbals) and still-missing crew; ordinary returned MIA records are excluded. Permanent history is included only when enabled. Dates or portraits that were never captured are marked unknown/unavailable. Embedded art needs no additional files or dependencies.
- The detail panel shows the selected Kerbal’s actual portrait when a usable stock flight portrait was captured. The flight-ready event starts a dedicated late-update scan, including on the launchpad. Each living crew member waits at least five real and game seconds from avatar availability (or the stock static overlay duration plus one second, whichever is longer). Capture prefers the actual avatar render target and requires two usable samples, rejecting blank frames and detectable high-frequency static. Hidden crew may use their existing IVA camera after the wait. Previously saved usable images remain available until a new one is captured. Casualty events copy the last accepted portrait rather than reading a potentially dying avatar. PNG images are saved with the scenario and individual casualty records (including the permanent archive). Detectable legacy static is rejected when loading/displaying it; an already deceased Kerbal without a valid saved portrait cannot be reconstructed. Crew lost before capture completes may have no portrait.
- The upper-right timer shows elapsed game time in completed days only (using the active KSP calendar) since the latest dated incident (missing or fatal) in the current timeline, including returned or respawned incidents. Archived alternate timelines and unknown dates are excluded. It follows time warp and quickloads; before any incident it shows None recorded. The Statistics tab fatality timer also shows completed days.

- Both the VAB and SPH have a world-space safety sign aligned horizontally with the stock building flag. It works independently of the memorial window and follows the current timeline and calendar. The four hanging green number cards have blank leading positions and the word DAYS to their right. The sign uses high-resolution, anti-aliased bold lettering and printed digits from artwork embedded in the DLL, on an uncompressed 2048 × 2048 texture with mipmaps and trilinear filtering. Its physical aspect ratio remains 3:2. Artwork can be regenerated using the optional Python/Pillow script under KerbalMemorial/Artwork; no font files or Python are needed for building or running the mod. The default sign width is 45% of the flag width, and the bottom edge is 2 m above the detected scenery floor. In `GameData/KerbalMemorial/AccidentSign.cfg`, configure each building independently: `widthMultiplier` is a fraction of the flag width; a positive `widthMeters` overrides it with an absolute width; `heightAboveFloor` sets the bottom-edge clearance in meters. If automatic floor detection fails, the sign uses world Y=0 and logs a warning; set `floorY` to override the floor reference. Set `heightAboveFloor = -1` to restore flag-relative placement using `gapMultiplier`. Horizontal and depth offsets remain multiples of the flag width. Restart KSP after editing the base configuration. In the memorial window, select **Sign Settings** to configure the VAB and SPH widths and bottom-edge floor clearances in meters. Moving a slider updates the current editor immediately and saves the changed field to `GameData/KerbalMemorial/PluginData/SignSettings.cfg`, overriding that field from AccidentSign.cfg. Close simply dismisses the window. The sliders step in 0.1 m increments, with widths from 1.0 to 5.0 m and floor clearances from 0.1 to 10.0 m. Values outside these ranges are clamped for display; opening the window does not save them, and moving a slider saves only its own field. Legacy width 0 and height -1 remain supported through the configuration files, but not through the sliders. Delete the override file with KSP closed to return to the base configuration. Exact placement requires in-game verification.

- Save-game mode (default): a ScenarioModule stores records in the save and follows quickload/revert history.
- Permanent history: enable in the memorial settings or stock difficulty settings. A separate archive is stored in `saves/<save>/KerbalMemorial/Casualties.cfg`. Records outside the current save are labelled Archived. Repeating a fatal launch after reverting is a new incident; loading the same already-recorded incident does not create another ID. Archive scope is the save folder; back it up or remove it before reusing that folder for an unrelated career. The option itself follows difficulty settings in the loaded save. Enable it before the launch, then save the settings. Disabling it hides the archive without deleting it.
- Confirmed deaths include explicit recognized crew-killed reports and Dead roster status. Missing alone is not counted as fatal. A fatality remains in history after respawn. A missing crew member who returns without death evidence is shown as Returned.
- Previously dead/missing crew can be imported; dates and circumstances are unknown, and no notification is generated for an import. Stock may have already removed old crew from its roster; those cannot be reconstructed. The import option also governs new, previously unseen casualties detected solely by the periodic scan.
- Captures loaded and unloaded crew context, with timestamp and quality. Event evidence can be absent or arrive in a different order, and context can be stale. No claim of exact cause is made without evidence. Unloaded-vessel speed and positions are whatever KSP exposes; G-force is omitted for unloaded craft. Roster removal alone is never treated as a death.
- Notes follow the current save unless permanent mode is enabled. Export includes all visible records, regardless of current search/filter. CSV files are written to the save's KerbalMemorial subfolder.
- Tracking is passive: no roster, difficulty penalties, part physics or respawn rules are modified.

## Other mod integration

Use a reference to KerbalMemorial.dll (optional reflection integration is also possible):

```csharp
KerbalMemorial.MemorialAPI.ReportDeath(crew, "Life support failure", "YourModName");
```

Call on the main Unity thread when a fatality is actually committed, before or near the roster update. This reports a confirmed casualty, but does not kill or change the Kerbal. It returns false when the tracker is unavailable. API version is `MemorialAPI.Version` (1).

No direct life-support adapter or KRASH simulation exclusion is included. In permanent mode simulated deaths can therefore be archived. There is no dynamic numeric badge on the toolbar icon; counts are shown in the memorial window. Overheating and G-force are not inferred merely from telemetry. English UI only in this initial version.

## Tests

`dotnet run --project Tests/LedgerTests.csproj` on a Windows .NET 4.8 development environment tests the pure ledger. The tests and the plugin require compilation locally; the distributed source was not compiled or run in KSP. See `Tests/ManualTests.md` for in-game verification.
