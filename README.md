# Kerbal Memorial

Kerbal Memorial is a KSP1 mod that honors fallen Kerbals by automatically tracking casualties, recording their causes of death, and displaying commemorative plaques on a customizable Memorial Wall. Each plaque features the Kerbal's portrait, name, date of death, and a randomly selected memorial inscription. The mod also provides detailed accident records, casualty statistics, CSV exports, optional permanent history across reverts, and a “Days Since Last Accident” safety sign in the VAB and SPH. KRASH simulations are automatically excluded, and other mods can report fatalities through an optional integration API.

## Usage

- Open the Memorial Wall from the stock or Blizzy toolbar. Select **Kerbal Memorial** at the bottom of the wall to review deceased, missing, returned and respawned Kerbals, inspect their last known vessel and flight circumstances, add notes, view statistics and export CSV.

- Drag the unused window background to move the memorial; use the corner to resize and the centered Close button at the bottom to dismiss it.

- The toolbar opens the resizable, draggable Memorial Wall directly. Its **Kerbal Memorial** button opens the main ledger, which can be closed independently. Closing the wall or toggling the toolbar off closes the entire interface. The original memorial artwork supplies the decorative heading, side panel and inscription; the fictional plaques are replaced with dynamic plaques containing the actual saved portraits, names, dates and loss status. Scroll to see every recorded fatality (including respawned Kerbals) and still-missing crew; ordinary returned MIA records are excluded. Permanent history is included only when enabled. Dates or portraits that were never captured are marked unknown/unavailable. Select Classic or Alternate (industrial metal) in **Settings / Help**, or use the wall’s Theme button. Both controls update the same per-save difficulty preference. Enable **Hide theme button on memorial wall** to hide that button without disabling theme selection in Settings / Help. Each theme has its own plaque typography and layout. Click any visible plaque to open the **Individual Memorial Plaque** first. Select **Details** on that plaque to open the separate **Kerbal Information** window with its portrait, summary, Show Full Details toggle and editable notes. Selecting a wall plaque again dismisses the previous details; closing the individual plaque or wall also closes its child windows. Closing the main ledger closes its Sign Settings window but leaves the wall and its plaque windows open. Closing Details leaves the plaque open. Both display only actual casualty data; the alternate artwork contains no example plaques or portraits. Embedded art needs no additional files or dependencies.

- The main Memorial tab defaults to a compact summary of the selected casualty: name/status, portrait, date fatality confirmed, cause, evidence, vessel name, part and body. Select **Show Full Details** beneath the detail panel to reveal the remaining information, including editable notes and flight history; **Hide Full Details** returns to the summary. The view choice is retained while browsing casualties in the current window instance.

- The detail panel shows the selected Kerbal’s actual portrait when a usable stock flight portrait was captured. The flight-ready event starts a dedicated late-update scan, including on the launchpad. Each living crew member waits at least five real and game seconds from avatar availability (or the stock static overlay duration plus one second, whichever is longer). Capture prefers the actual avatar render target and requires two usable samples, rejecting blank frames and detectable high-frequency static. Hidden crew may use their existing IVA camera after the wait. Previously saved usable images remain available until a new one is captured. Casualty events copy the last accepted portrait rather than reading a potentially dying avatar. PNG images are saved with the scenario and individual casualty records (including the permanent archive). Detectable legacy static is rejected when loading/displaying it; an already deceased Kerbal without a valid saved portrait cannot be reconstructed. Crew lost before capture completes may have no portrait.

- The upper-right timer shows elapsed game time in completed days only (using the active KSP calendar) since the latest dated incident (missing or fatal) in the current timeline, including returned or respawned incidents. Archived alternate timelines and unknown dates are excluded. It follows time warp and quickloads; before any incident it shows None recorded. The Statistics tab fatality timer also shows completed days.

- Click a wall plaque to see the individual commemorative plaque in the current theme. Choose **Details** to see the Kerbal Information window. The bottom inscription panel displays the inscription assigned when a fatality is confirmed. Add UTF-8 text files to `GameData/KerbalMemorial/PluginData/Inscriptions/`, with one message per line. Markdown headings, blank lines and horizontal rules are ignored; bullet and numbered list prefixes and enclosing emphasis are removed. All `.txt` files contribute to one random message pool, read at the time of each new fatality and during backfill of older records. The selected message is saved in the casualty record and permanent archive (if enabled), separately from editable notes. All confirmed fatalities without an inscription (including imported, archived and respawned fatalities) receive one automatically. Missing crew receive an inscription only if a fatality is confirmed. If the pool is empty, the original notes remain visible and the tracker retries every ten real-time seconds and on save. Existing inscriptions are never replaced. Inscriptions use large centered text within the middle 80% of the lower panel, wrapping as needed; exceptionally long inscriptions can be scrolled.

- Both the VAB and SPH have a world-space safety sign aligned horizontally with the stock building flag. It works independently of the memorial window and follows the current timeline and calendar. The four hanging green number cards have blank leading positions and the word DAYS to their right. The sign uses high-resolution, anti-aliased bold lettering and printed digits from artwork embedded in the DLL, on an uncompressed 2048 × 2048 texture with mipmaps and trilinear filtering. Its physical aspect ratio remains 3:2. Artwork can be regenerated using the optional Python/Pillow script under KerbalMemorial/Artwork; no font files or Python are needed for building or running the mod. The default sign width is 45% of the flag width, and the bottom edge is 2 m above the detected scenery floor. In `GameData/KerbalMemorial/AccidentSign.cfg`, configure each building independently: `widthMultiplier` is a fraction of the flag width; a positive `widthMeters` overrides it with an absolute width; `heightAboveFloor` sets the bottom-edge clearance in meters. If automatic floor detection fails, the sign uses world Y=0 and logs a warning; set `floorY` to override the floor reference. Set `heightAboveFloor = -1` to restore flag-relative placement using `gapMultiplier`. Horizontal and depth offsets remain multiples of the flag width. Restart KSP after editing the base configuration. In the memorial window, select **Sign Settings** to configure the VAB and SPH widths and bottom-edge floor clearances in meters. Moving a slider updates the current editor immediately and saves the changed field to `GameData/KerbalMemorial/PluginData/SignSettings.cfg`, overriding that field from AccidentSign.cfg. Close simply dismisses the window. The sliders step in 0.1 m increments, with widths from 1.0 to 5.0 m and floor clearances from 0.1 to 10.0 m. Values outside these ranges are clamped for display; opening the window does not save them, and moving a slider saves only its own field. Legacy width 0 and height -1 remain supported through the configuration files, but not through the sliders. Delete the override file with KSP closed to return to the base configuration. Exact placement requires in-game verification.

- Save-game mode (default): a ScenarioModule stores records in the save and follows quickload/revert history.

- Permanent history: enable in the memorial settings or stock difficulty settings. A separate archive is stored in `saves/<save>/KerbalMemorial/Casualties.cfg`. Records outside the current save are labelled Archived. Repeating a fatal launch after reverting is a new incident; loading the same already-recorded incident does not create another ID. Archive scope is the save folder; back it up or remove it before reusing that folder for an unrelated career. The option itself follows difficulty settings in the loaded save. Enable it before the launch, then save the settings. Disabling it hides the archive without deleting it.

- Confirmed deaths include explicit recognized crew-killed reports and Dead roster status. Missing alone is not counted as fatal. A fatality remains in history after respawn. A missing crew member who returns without death evidence is shown as Returned.

- Previously dead/missing crew can be imported; dates and circumstances are unknown, and no notification is generated for an import. Stock may have already removed old crew from its roster; those cannot be reconstructed. The import option also governs new, previously unseen casualties detected solely by the periodic scan.

- Captures loaded and unloaded crew context, with timestamp and quality. Event evidence can be absent or arrive in a different order, and context can be stale. No claim of exact cause is made without evidence. Unloaded-vessel speed and positions are whatever KSP exposes; G-force is omitted for unloaded craft. Roster removal alone is never treated as a death.

- Notes follow the current save unless permanent mode is enabled. Export includes all visible records, regardless of current search/filter. CSV files are written to the save's KerbalMemorial subfolder.

- Tracking is passive: no roster, difficulty penalties, part physics or respawn rules are modified.

### Stock cause classification

Stock overheating, over-G, over-pressure, ground collision, splashdown, collision, part failure and joint break events are correlated with the actual casualty. A report alone never creates a fatality. EVA casualties receive specific impact, collision, overheating or water-impact labels when supported, otherwise an unknown-cause EVA label if no better cause is available. Unloaded vessel ground impacts are tracked through the stock vessel ground collision event. Generic collision reports identify a vessel collision only when the reported counterpart name matches exactly one other loaded vessel. Aerodynamic breakup is probable, based on a positive joint break force and at least 10 kPa dynamic pressure while airborne. These are heuristics, not definitive accident investigations. External API causes remain authoritative and KRASH exclusions still apply. Existing historical records are not reclassified retroactively.

## Other mod integration

Copy `Integration/KerbalMemorialWrapper.cs` into your own KSP1 mod project and include it in compilation. It uses reflection to bind the existing API to a cached delegate and does not require a reference to KerbalMemorial.dll.

```csharp
using KerbalMemorialIntegration;

// When your mod commits a real fatality:
bool recorded = KerbalMemorialWrapper.ReportDeath(crew, "Life support failure", "YourModName");
```

`IsAvailable` checks whether a compatible API is loaded, not whether the tracker is ready. `ReportDeath` returns false if the mod is missing, the API is incompatible, the tracker is unavailable, KRASH is active or the report fails. Call on the main Unity thread when a fatality is actually committed, before or near the roster update. This reports a confirmed casualty, but does not kill or change the Kerbal. Do not make your mod's gameplay depend on the return value. The original direct API `KerbalMemorial.MemorialAPI.ReportDeath` remains unchanged (API version 1).

### Life Support Integration

No direct life-support adapter is included. KRASH simulations are excluded automatically, including permanent history and external API reports. The optional integration reads the KRASH persistent simulation flag via reflection; if that flag is unavailable, it checks for KRASHRevert.sfs in the active save folder. KRASH is not required. Existing casualty records are not retroactively deleted. There is no dynamic numeric badge on the toolbar icon; counts are shown in the memorial window. Overheating and G-force are not inferred merely from telemetry. English UI only in this initial version.

It is planned to submit PRs for the following life support mods to add direct integration, so that they can report confirmed deaths to the memorial tracker. The mods are:

- IFI-Life-Support
- KickLifeSupport
- Snacks
- TacLifeSupport
- USI-LS

