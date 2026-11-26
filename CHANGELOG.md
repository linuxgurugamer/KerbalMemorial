# 0.1.0.39
- Make the automatic death plaque delay configurable per save in Settings / Help and stock difficulty settings: 0-5 real-time seconds, default 1.5, in 0.1-second steps. Each new death uses the current delay; Flight Results deferral remains unchanged.

# 0.1.0.38
- Randomly draw distinct inscriptions without replacement in separate persisted missing/deceased cycles. Seed old saves from assigned messages and preserve cycle progress across save/reload and permanent history.
- Add per-save Show memorial plaque on death option (default on). Disabled presentation does not defer Flight Results or queue old deaths.
- Wait one real-time second after each confirmed fatality before showing its plaque; keep Flight Results held during the delay and the existing sequential queue.

# 0.1.0.37
- Automatically show each newly confirmed death plaque, one at a time. Closing acknowledges that record; historical imports and duplicate notifications do not create popups.
- Hold the stock Flight Results dialog visually until the queue is acknowledged; preserve its data and remove the temporary visibility gate. Handle stock crash pause while results are held.

# 0.1.0.36
- Use separate PluginData/Inscriptions/Deceased and Missing pools. Persist random missing inscriptions, upgrade old fixed messages when a pool is available, and keep fallback text until then. Preserve assigned messages and switch pools on confirmed death.

# 0.1.0.35
- Synchronize the stock and Blizzy toolbar states through shared wall open/close callbacks. Closing child windows leaves the toolbar active; pause and F2 preserve the open state.

# 0.1.0.34
- Assign "Missing, but not forgotten" to unconfirmed missing crew, including existing records and archives. Replace temporary inscriptions on confirmed death and clear them on return.

# 0.1.0.33
- Backfill missing inscriptions when the Memorial Wall loads its list, including older saved and archived fatalities; immediately assign inscriptions to newly imported fatalities. Preserve saved messages and retry empty pools on the existing schedule.

# 0.1.0.32
- Replace README.md with the updated supplied text.

# 0.1.0.31
- Remove build.ps1.
- Replace README.md with the supplied text.

# 0.1.0.30
- Add stock-event classifications for overheating, excessive G-force/pressure, water impact, vessel collisions, structural failure, aerodynamic breakup, EVA accidents and unloaded-vessel terrain impacts.
- Correlate evidence by crew, part/vessel and a short real-time and game-time window; prioritize specific evidence and retain external API causes.
- Joint-break aerodynamic classification uses a probable 10 kPa dynamic-pressure heuristic; ambiguous collision reports remain generic collisions.

# 0.1.0.29
- Open the Memorial Wall first from either toolbar. Add a Kerbal Memorial button to the wall footer and remove the Memorial Wall button from the main window.
- Closing the main window leaves the wall open; closing the wall or toggling the toolbar off closes all associated windows.

# 0.1.0.28
- Add a standalone reflection-based KerbalMemorialWrapper.cs for optional death reporting by other mods; preserve the existing public API.
- Open the individual plaque directly from either memorial wall theme. Add Details to the plaque and remove View Memorial Plaque from Kerbal Information. Close child windows when the plaque or wall closes.

# 0.1.0.27
- Exclude KRASH simulations from casualty tracking, roster imports, external death reports and permanent archive updates using optional runtime detection.
- Enlarge individual plaque inscriptions in both themes, wrap within the central 80% of the panel and retain scrolling for exceptionally long messages.

# 0.1.0.26
- Automatically backfill missing inscriptions for existing confirmed fatalities, including historical imports, respawned fatalities and archived records. Retry when files become available and preserve existing inscriptions across live/archive copies.

# 0.1.0.25
- Choose and save a random memorial inscription from PluginData/Inscriptions/*.txt when a new fatality is confirmed. Skip Markdown headings and list formatting. Keep the inscription separate from casualty notes.
- Display saved inscriptions on individual plaques in either theme; retain notes for older records without an inscription. Include the inscription in CSV exports.

# 0.1.0.24
- Hide all Memorial windows while KSP is paused; restore their previous open state on resume. Keep F2 visibility independent of pause state.

# 0.1.0.23
- Add a View Memorial Plaque button to the Kerbal Information window. The new individual plaque follows the current wall theme and displays the selected casualty's saved portrait, name, date, and notes.
- Use the supplied Classic individual plaque art with sample data removed, and adapt the existing cleaned Alternate industrial plaque art into a matching large plaque template.

# 0.1.0.22
- Move the toolbar icon textures into PluginData/Textures and update their paths.
- Rename the toolbar field to toolbarController.

# 0.1.0.21
- Add Classic/Alternate theme selection and a hide-theme-button option to Settings / Help and stock difficulty settings, using the existing per-save preferences.
- Synchronize the wall theme button with those settings.
- Make plaques in both wall themes clickable; open a separate Kerbal Information window with the shared summary/full-detail view, portrait and notes.

# 0.1.0.20
- Simplify the main Memorial tab casualty detail panel to name/status, portrait, fatality confirmation date, cause, evidence, vessel name, part and body. Add a Show/Hide Full Details button for the remaining fields, notes and history.

# 0.1.0.19
- Remove all sample portraits, names and dates from the alternate wall and plaque artwork.
- Give the alternate theme its own plaque layout, white left-aligned typography and silver divider; remove translucent sample-plaque masking and Classic plaque fallback.
- Reset scroll position when switching themes.

# 0.1.0.18
- Fix the invalid literal newline in the Classic Memorial Wall fallback heading.

# 0.1.0.17
- Add an alternate Memorial Wall theme based on the user-supplied industrial wall image.
- Keep the existing wall as the Classic theme and add a Theme toggle button in the Memorial Wall window to switch between Classic and Alternate styles.
- Use the supplied image as the alternate wall frame/background and a cropped plaque style while continuing to generate the actual casualties dynamically from the ledger.

# 0.1.0.16
- Replace the memorial wall background tile drawing with a single procedurally generated stone panel so the wall surface appears continuous instead of visibly repeating in rectangular blocks.
- Regenerate the wall background texture at the current wall size and cache it until the window size changes materially.

# 0.1.0.15
- Fix the Memorial Wall shrinking and flickering by using a fixed-size ClickThroughBlocker GUIWindow instead of GUILayoutWindow; preserve the requested dimensions independently of drag events.
- Populate the plaque list immediately when opening, even before the next Layout event.

# 0.1.0.14
- Add a separate, scrollable Memorial Wall window using decorative artwork cropped from the original Kerbal Memorial Wall image, with dynamic bronze plaques containing saved portraits, names, dates and loss status.
- Include confirmed fatalities (including respawns) and currently missing crew; exclude ordinary returned MIA records. Honor the permanent history setting.

# 0.1.0.13
- Replace the old pixel-grid lettering and seven-segment numbers with smooth anti-aliased bold type and printed number cards.
- Embed the high-resolution board and digit atlas in the DLL; use an uncompressed 2048 x 2048 power-of-two texture with mipmaps, trilinear filtering, 16x anisotropic filtering and a small negative mip bias. Keep the existing 3:2 physical aspect ratio, card behavior and placement settings.

# 0.1.0.12
- Apply and save each sign slider adjustment immediately; remove Apply and preserve untouched settings.
- Add DAYS to the right of the four hanging number cards, keeping the lower two lines unchanged.

# 0.1.0.11
- Replace the four sign settings text fields with sliders in 0.1 m increments: width 1.0–5.0 m, height 0.1–10.0 m. Show and save one decimal place.
- Clamp legacy values to slider bounds when opening the window; only Apply writes overrides. Legacy values are still supported through the configuration files.

# 0.1.0.10
- Add a separate Accident Sign Settings window with independent VAB/SPH width in meters and floor clearance controls.
- Apply and save changes without restarting KSP; update the existing editor sign transform without regenerating its artwork. Store installation-level overrides in PluginData/SignSettings.cfg.

# 0.1.0.9
- Update jenkins.txt to the supplied build configuration.

# 0.1.0.8
- Move the editor safety sign to 2 m above the detected scenery floor (bottom edge), retaining its alignment with the flag.
- Add independent width in meters and floor clearance settings for VAB and SPH, plus an optional floor Y override. Keep flag-relative size and legacy flag-gap placement available.

# 0.1.0.7
- Reduce the default VAB and SPH safety sign width and height by 50%, preserving the aspect ratio, artwork and gap below the flag.

# 0.1.0.6
- Replace the accident sign's first-line number string with four hanging day cards using white seven-segment digits on green cards; leading zeros now appear as blank cards.
- Increase the procedural sign texture resolution and enable mipmaps, trilinear filtering and anisotropic filtering to reduce visible pixelation in the editor scene.

# 0.1.0.5
- Add a three-line, dynamically updated safety sign beneath the stock background flag in the VAB and SPH.
- Display completed game days since the latest dated current-timeline incident; before the first recorded accident count from UT zero, and use dashes if the date is unknown.
- Add separate sign placement settings for both editors.

# 0.1.0.4
- Display time since the last incident and the Statistics tab fatality timer in completed game days only, using the active KSP calendar.

# 0.1.0.3
- Wait for each living Kerbal's avatar to finish the stock static overlay before capturing; require two usable frames and reject blank/static images.
- Prefer the actual avatar render target; casualty events only use the previously saved portrait. Reject detectable legacy static on load/display.

# 0.1.0.2
- Center Close at the bottom of the window; retain Export CSV and corner resizing.
- Capture each active vessel crew portrait starting at flight readiness, retry as stock renders it, and refresh saved portraits on flight entry. Save the image with the scenario and copy it into each casualty record.
- Show live in-game time since the latest dated current-timeline incident in the upper right on all tabs.

# 0.1.0.1
- Allow dragging from unused window areas; preserve normal controls and corner resizing.
- Replace X with a labelled Close button.
- Capture and persist actual crew portraits; show the selected casualty portrait in the detail panel.

# 0.1.0.0
- Initial source release: casualty ledger, stock event tracking, loaded/unloaded context, scenario persistence, optional permanent archive, toolbar window, statistics, notes, CSV and integration API.
