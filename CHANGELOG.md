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
