# KSP integration verification

Use a disposable save, with and without Missing Crew Respawn enabled. Back up the save before testing.

1. Install, open the memorial in Space Center, VAB/SPH, Flight and Tracking Station. Test both toolbars, close, drag/resize, F2, and scene changes.
2. Crash a single-crewed pod, then a multi-crewed vessel; confirm exactly one incident per Kerbal, correct context and no false survivors. Compare MIA and permanent death behavior to the stock roster.
3. Kill an EVA Kerbal. Destroy an uncrewed part while a crewed part survives; no casualties for survivors. Decouple/dock/recover a vessel without deaths; no casualties.
4. Terminate a crewed unloaded vessel in Tracking Station; verify context and cause when event ordering permits. Recover a tourist normally; roster removal should not create a death.
5. Allow a missing Kerbal to respawn; previous incident remains, and subsequent death creates another record. Rename a Kerbal and inspect old records.
6. Save/reload and change scenes; verify record IDs and notes remain stable. Quickload a save preceding a death with permanent mode off; death should disappear. Enable permanent mode and save; repeat and verify prior death is Archived, new death has a distinct ID. Switch to another career and verify isolation.
7. Test life support via explicit API and separately a mod that changes roster status. Check duplicate suppression when multiple events fire, and unknown cause when no evidence exists.
8. Export CSV with commas, quotes and multiline notes; verify columns and UTF-8. Check settings, empty and filtered views, and archive read/write errors.

9. Drag the window by its title, unused background and footer on all tabs. Verify buttons, text fields, scrollbars and resize handle still work. Close using the Close button and verify the toolbar state.
10. Launch with crew portraits visible for several seconds, then lose a crew member. Verify the correct portrait appears, survives scene changes, save/reload and permanent-mode revert, and remains attached after renaming. Check missing images for pre-existing deaths and test selecting different entries repeatedly.

11. On all tabs and at minimum and expanded window sizes, check that Close is centered at the bottom and does not interfere with Export CSV or the resize handle.
12. Launch a crewed vessel with the memorial closed. Wait for stock portraits to render on the pad, then save. Verify CREW_PORTRAIT entries for the crew in the scenario. Test crew larger than the gallery capacity, cycling visible portraits if stock has not rendered hidden crew. Check loaded and switched vessels, IVA-less parts, and failed capture fallback to an older image. Confirm images survive recovery, save/reload and subsequent deaths, and old casualty portraits are not overwritten by a later launch.
13. Check the upper-right timer before any incident, after MIA/death, after return/respawn, during time warp, and after save/reload/revert. In permanent mode, archived-only incidents must not affect elapsed time. Imported unknown dates must not generate a false elapsed time.

13. Launch a vessel with no saved portraits. During the initial stock static overlay, save and verify no new CREW_PORTRAIT image is committed. Wait until the faces have rendered for at least five seconds; verify each crew image shows the correct face (not static). Test pausing, slow loading, switching active vessels, crew outside the gallery, and dying before and after the capture completes. Reload a pre-0.1.0.3 save with static portraits and verify detectable noise is not displayed or reused; launch surviving crew to refresh their portraits. Verify a valid portrait from a previous flight survives an early casualty.

- Verify the upper-right timer and Statistics fatality timer show only whole days: 0 days before a full day, 1 day after exactly one day, and total days even across years. Test both stock calendar modes and custom calendars if installed.

## Editor safety sign (0.1.0.5)
- Enter the VAB and SPH at each facility level. Verify one sign is centered below the interior flag, with readable two-line heading, correct facing, and no clipping or overlap with the wall or flag. Orbit and zoom the camera; the board remains in world space and is occluded by scenery or parts normally.
- Switch between editors, change flag, return to the Space Center and re-enter. Verify no duplicate boards or leftover objects. The sign has no collider and does not interfere with part placement.
- Start a fresh game: sign reads 0 DAYS; advance a day and check 1 DAYS. Trigger a missing or fatal casualty, return to editor, and check 0 DAYS. Advance multiple game days, reload/revert, and verify the sign follows the current timeline. Test with custom calendar day lengths and old saves with only undated casualties (--- DAYS).
- Close the memorial window; sign remains visible. Adjust individual facility offsets in AccidentSign.cfg and restart the game to verify the sign can be fine-tuned without code changes.
- Inspect KSP.log for the accident-sign placement message or a warning if no active flag renderer is available.

- Verify the VAB/SPH safety sign digits render as four cream-colored cards, with blank leading cards for values under 1000 and visibly smoother text/edges than the previous release.

- Verify the default sign is half its previous width and height in both editors; the top gap below the flag remains unchanged.

## Editor sign floor placement (0.1.0.8)
- Verify the sign bottom is approximately 2 m above the floor in VAB and SPH at each upgrade level, and it remains aligned with the flag. Check KSP.log for the detected floor Y or fallback warning.
- Test with and without a vessel in the editor. Confirm vessel parts cannot be used as the floor reference.
- Set VAB widthMeters = 1.5 and SPH widthMeters = 2.5; restart and verify independent widths and preserved aspect ratio. Set widthMeters = 0 to restore relative sizing.
- Change heightAboveFloor, test a floorY override and test legacy placement with heightAboveFloor = -1. Verify resizing does not change the clearance above the floor.

## Sign settings window
- In VAB, open Memorial > Sign Settings. Move the VAB width and clearance sliders; verify that each 0.1 m change moves/resizes the sign without Apply, with no duplicate board or change in aspect ratio. Verify the clearance remains fixed when changing width.
- Adjust SPH width and clearance while in VAB; the VAB stays unchanged. Switch to SPH and verify the independently saved values. Restart KSP and check both facilities.
- Move each slider to both endpoints; verify width 1.0–5.0 and height 0.1–10.0 with one decimal place. With legacy width 0 and height -1, open and close without moving any slider; verify no override changes. Move one slider and check only its field is written, leaving the other three settings intact.
- Move a slider and close; reopening shows the saved value. Verify there is no Apply button, and F2 and closing the parent hide the child window. Verify background dragging and click-through protection.
- With an unwritable settings directory, changing a slider reports failure and leaves the previous value and sign layout unchanged. Delete PluginData/SignSettings.cfg while KSP is closed to restore AccidentSign.cfg defaults.
- Verify DAYS SINCE LAST ACCIDENT is above the four cards and the mascot and hazard stripes remain unobscured, including for 0, 42, 9999 and unknown-date dashes. Verify no baked-in example numbers or ghost digits remain, and each card retains its own texture after repeated updates.

## Smooth sign artwork (0.1.0.13)
- Inspect lettering and printed numerals in both buildings at close range, at normal editor zoom, and at oblique angles. Confirm the 5×7 pixel-block lettering is gone and cards retain their blank leading positions.
- Check 0, 9, 42, 1000, 9999, over-limit counts and unknown-date dashes, including a transition back to 0; old digits must be fully cleared.
- Adjust sign width and height; confirm unchanged 3:2 aspect ratio, no texture compression artifacts, and no new external artwork files needed in GameData. Check at different KSP texture quality settings.

- Open Memorial Wall directly from the toolbar. Verify original decorative artwork, no fictional plaques, actual portraits/names/dates, missing and respawned labels, unknown data fallback, archive visibility, chronological scrolling with many records, dragging/resizing/centered Close, F2 hiding, and scene cleanup. Verify returned ordinary MIA is excluded and toggling permanent history changes archived plaques.

## Memorial Wall window sizing (0.1.0.15)
- Open the wall with zero casualties and again with several casualties. Without moving the mouse, verify it remains at its intended full size (1060 x 760 on a sufficiently large screen).
- Drag the title or unused background, release the mouse and wait. Verify no shrinking, disappearance or flicker; scroll, resize from the lower-right handle, and verify the new size remains stable. Close and reopen; confirm the chosen size is retained for the current scene.
- Test on smaller resolutions and after changing resolution; the window must stay within the screen. Verify the main window and sign settings still work and click-through is blocked.

- Open the Memorial Wall and verify the stone background appears as one continuous surface with no obvious repeating rectangular tiles, including after resizing the window.

- Open the Memorial Wall and toggle between Classic and Alternate themes; verify the Alternate theme uses the supplied industrial wall styling, dynamic plaques, and remains scrollable/resizable.

- Switch to Alternate with zero casualties: verify no sample plaques, names, dates or portraits are visible. With casualties, verify white left-aligned text, silver divider, correct saved portraits and dates, and a blank inset for unavailable portraits (with an explanatory label). Switch back to Classic and check bronze lettering is unchanged. Scroll and resize both themes.

## Compact casualty details (0.1.0.20)
- Open the Memorial tab and select a casualty. Verify that the default panel contains only the name/status, saved portrait and six requested labeled fields. No notes, confidence, timeline, telemetry or career history should appear until Show Full Details is selected.
- Select Show Full Details and verify every previous field and editable notes remain accessible; select Hide Full Details and check the compact view is restored. The button remains outside the scrolling detail panel. Switch records, including missing or undated fatalities, and verify unknown values and portraits are handled as before.
- Check that both Memorial Wall themes, CSV export and sign settings remain unchanged.

### Wall settings and plaque information (0.1.0.21)
- In Settings / Help choose Alternate; verify the open wall switches immediately. Switch back via the wall button and verify Settings / Help shows Classic.
- Hide the theme button while the wall is open; verify it disappears and the theme remains selectable in settings. Re-enable it. Save the game and reload; verify both preferences persist.
- Click the portrait, name, and background of a plaque in each theme, including a plaque revealed by scrolling. The same casualty opens as an individual plaque first; click Details to open its information window with portrait and compact details. Verify no click-through or accidental wall dragging.
- Open full details and edit notes for a current or archived record; verify the main list and saved records reflect the edit. Click a different wall plaque; verify the previous details close, the correct individual plaque opens, and Details opens its reset summary.
- Verify scrollbar, theme button, drag, resize, Close and F2 work with the information window open; closing the wall closes the information window; closing the main ledger does not.
- Verify quickload/removal of a record cannot cause the popup to display a different casualty or edit a stale record.

- Verify both stock and Blizzy toolbar icons load from PluginData/Textures and open/close normally.

- Click a lost Kerbal on either wall theme to open the individual plaque. Verify the portrait, name, date and notes belong to that record and no sample data is visible. Check unknown date/missing portrait, blank and long notes, theme switching, F2, dragging, close, opening a different casualty, and small screen fit. Closing Kerbal Information leaves the individual plaque open; closing the plaque or wall closes its child windows.

## Pause visibility (0.1.0.24)
- Open the main Memorial, Sign Settings, Memorial Wall, Kerbal Information and Individual Memorial Plaque windows in flight. Pause with Escape; verify all five disappear. Resume; verify the same windows, selections and scroll positions return.
- Repeat with F2 hidden before pausing. Resume while F2 is still hidden; verify none appear until the UI is shown again. Test pause and F2 in reverse order too.
- Pause with all windows closed and resume; verify none open automatically. Check pause/resume in map view and the editor/space center/tracking station wherever KSP emits pause events. Verify dragging/resizing works after resuming and scene changes do not leave stale pause state.

- Add multiple .txt files in PluginData/Inscriptions/Deceased and PluginData/Inscriptions/Missing with Markdown headings, blank lines, bullet lists and plain messages. Trigger a casualty: confirm only valid messages are used, duplicate fatality events do not change the inscription, both themes display it, and save/reload and permanent archive preserve it. Check missing crew without confirmed fatalities get an inscription from the Missing folder; historical imports, old saved fatalities and archived fatalities receive inscriptions automatically. Verify empty folders do not interrupt casualty tracking; add a file during play and confirm missing inscriptions are assigned within ten seconds. Verify save/reload, archive reconciliation and respawned fatalities preserve assigned messages. Verify notes remain independently editable.

## KRASH and inscription layout (0.1.0.27)
- With and without permanent history enabled, launch a KRASH simulation, kill a crew member, wait for a roster poll, and end/restart the simulation. Verify no new casualty, notification, inscription, archive entry or accident-counter reset. Test a death immediately after launching the simulation and an external ReportDeath call during simulation.
- Launch a real flight afterward and verify normal fatality capture, inscriptions and archive writes resume. Repeat without KRASH installed. Check the KSP log for reflection warnings.
- Open individual plaques in both themes with a short inscription, a longer sentence and several lines of text. Confirm large centered lettering, wrapping at roughly 80% of the panel width, no clipping, and scrolling only when necessary. Repeat at a smaller screen resolution.

## Optional integration wrapper and plaque navigation (0.1.0.28)
- Copy Integration/KerbalMemorialWrapper.cs into a test mod without referencing KerbalMemorial.dll. With Kerbal Memorial absent, verify IsAvailable and ReportDeath return false without errors. Install the mod and verify IsAvailable, then report an actual death with a source and cause; verify true, one casualty, and the supplied cause/evidence. Test invalid inputs, unavailable tracker, KRASH simulation and a repeated report for the same casualty.
- For both wall themes, click a plaque: only the individual plaque opens. Click Details: the matching information window opens above it, and contains no View Memorial Plaque button. Close Details; the individual plaque stays open. Click a different wall plaque while Details is open: the old Details closes and the new plaque opens. Verify closing the plaque or wall closes its descendants; closing the main ledger closes only its Sign Settings child, and pause/F2 still hide and restore open windows.

## Wall-first navigation (0.1.0.29)
- Click either toolbar icon: only the Memorial Wall opens. Click Kerbal Memorial in its footer: the main ledger opens, with no Memorial Wall button beside its tabs. Verify the wall remains open and the toolbar stays active. Close the ledger; verify the wall and its plaque windows remain open and the ledger’s Sign Settings window closes. Reopen the ledger from the wall; verify its previous position is retained.
- Open the ledger, Sign Settings and a wall plaque with Details. Close the wall: all associated windows close and the toolbar turns off. Repeat by toggling the toolbar off. Toggle on again: only the wall opens. Verify the wall works with an empty ledger, both themes, hidden theme button, pause/F2 and scene changes.

## Stock cause classification (0.1.0.30)
- Overheat an occupied part; verify overheating and temperature evidence, and that generic part destruction does not replace it. Repeat in EVA.
- Enable stock part G/pressure limits and exceed them on an occupied part; verify respective failure classifications only if a casualty occurs.
- Test fatal splashdown, ground impact and vessel collisions (including EVA); ambiguous collision targets must remain generic.
- Break an occupied part joint under high dynamic pressure and at low pressure, with a casualty; verify probable aerodynamic breakup or structural failure respectively. Ordinary staging without a casualty must not add records.
- Allow an unloaded crewed vessel to hit terrain; verify unloaded terrain impact and preserved proto-part details.
- Test deaths with only EVA context and without context; verify unknown-cause EVA and ordinary unknown fallbacks.
- Trigger evidence on an unoccupied or unrelated part and verify no unrelated death is reclassified. Confirm explicit wrapper reports remain authoritative. Repeat tests in KRASH with permanent history on; verify no casualties or evidence leaks.

- Load a save containing old fatalities with missing or whitespace-only inscriptions. Add a valid inscription file, then open the Memorial Wall immediately (without waiting for the ten-second retry). Verify the first individual plaque already shows its assigned message. Close and reopen the wall, save/reload, and check that existing messages never change. Repeat with permanent history enabled, including archive-only fatalities and live/archive copies. Verify no assignments or archive writes occur during KRASH.

## Missing crew inscriptions (0.1.0.34)
- With no inscription files, load existing missing crew and open the wall. Confirm both themes show the fixed message and the saved record contains it. Confirm a death (including a crew-killed event with Missing roster status): the temporary message must disappear, and a normal inscription must be assigned when the pool becomes available. Return another missing crew member and verify the temporary message is cleared. Repeat with permanent history enabled and archive-only records.

### Toolbar close synchronization
- With only the stock toolbar enabled, open the wall, close using its Close button, and verify the toolbar is deselected. The very next toolbar click must reopen the wall. Repeat with only Blizzy enabled, then with both enabled (open using one and close/reopen using the other). Blizzy uses the same icon in both states; verify by click behavior.
- Open the wall and all child windows. Close each child independently and verify the wall and toolbar stay active. Close the wall and verify every child closes and the next toolbar click opens only the wall. Repeat after pause/unpause, F2 and a scene change.

## Separate inscription pools (0.1.0.36)
- Put different messages in Inscriptions/Deceased and Inscriptions/Missing and a third message in the parent directory. Confirm new, historical and archived fatalities use only Deceased messages; unconfirmed missing records use only Missing messages. Test both themes and CSV.
- Load a save with the old fixed missing inscription, verify it upgrades once, then save and reload. Test a missing file containing the literal old fixed text: it must still be treated as an assigned random message. Verify permanent live/archive copies reuse the same compatible message, and different timeline statuses never exchange messages across pools.
- Remove both pools, confirm fallback missing text and notes for fatalities; add files during play, reopen the wall and verify backfill. Existing assigned messages must not reroll. Confirm death while Missing (including ReportDeath), then return a separate missing crew member; verify replacement from Deceased and clearing on return. Confirm no new assignments during KRASH.


## Automatic fatality plaques and Flight Results (0.1.0.37)
- Kill one Kerbal in flight without destroying the vessel, with the toolbar off. The automatic individual plaque opens with the correct saved name, portrait, date and inscription. Details works; Close acknowledges it without changing the toolbar.
- Destroy an occupied vessel with several Kerbals. Verify each confirmed death opens once, in recording order, one at a time; the stock Flight Results do not render or accept clicks until the final plaque closes. Verify the original results and exit/revert buttons work afterwards, including when stock pauses the game.
- Repeat with the wall, ledger and a manually selected plaque already open. They temporarily yield to the automatic sequence and return afterwards with their selection and toolbar state intact. Toggling the toolbar off must not dismiss an automatic plaque.
- Test unconfirmed MIA, subsequent confirmation, external API report, respawn followed by another death, and duplicate events. Only newly confirmed deaths queue. Import existing fatalities and enable permanent history; neither should replay old plaques. Test KRASH, quickload/revert during an outstanding sequence and scene exit; no stale plaques or suppressed stock UI remain.
- Test F2 and Escape with a surviving vessel; automatic plaques hide and resume normally. When stock Flight Results is held, allow acknowledgement despite stock pause/HUD hiding. Test F3 while a sequence is pending; the results dialog is deferred too. Test an uncrewed vessel loss; stock results remain unchanged.
- Inspect the Unity hierarchy for KSP.UI.Dialogs.FlightResultsDialog and its canvas groups during destruction. Verify rendering, interaction and restoration, including any stock animations; the hold is a visual gate, not a patch to the stock spawning method.


## Non-repeating inscriptions and optional delayed plaques (0.1.0.38)
- Use three distinct messages (including a duplicate line in another file) in each status folder. Trigger four new casualties of each status. The first three in each pool must be unique; the fourth may start a new cycle. Save/reload after two assignments, and verify the third uses the remaining message. Repeat with permanent history and revert. Older assigned messages should seed usage and never change.
- Return a missing crew member, or confirm their death, midway through the missing cycle. Their original missing message remains used until the cycle is exhausted. Remove the text files temporarily and restore them; an empty pool must not reset usage. Add another distinct message before exhaustion and verify it is eligible before any repeat.
- Toggle Show memorial plaque on death off in the stock difficulty settings or Settings / Help. Destroy an occupied vessel: casualties and inscriptions should be recorded normally without automatic plaques or Flight Results suppression. Manual plaques remain available. Re-enable the setting and verify no old deaths replay.
- With the option on, verify each automatic plaque appears no sooner than its configured real-time delay after the confirmed death (default 1.5 seconds). For staggered deaths each plaque has its own deadline; for simultaneous deaths the second is ready as soon as the first is dismissed, provided its delay has elapsed. Flight Results must remain hidden during the initial delay, including when stock pauses flight. Verify F2, ordinary pause, quickload and scene changes remain safe.

## Configurable death plaque delay (0.1.0.39)
- On a fresh or older save without the new setting, verify the default is 1.5 seconds. Change the slider in Settings / Help and in stock difficulty settings; both must display the same saved value in 0.1-second steps, from 0 to 5. Save and reload and verify persistence.
- Test 0, 1.5 and 5 seconds with single and multiple fatalities. A zero delay has no intentional wait; the stock dialog gate must still hold Flight Results until the queue is acknowledged. For deaths separated in time, each deadline uses the setting at confirmation. Changing the setting affects newly queued deaths only. Disable automatic plaques and verify delay has no effect on stock results.


## Automatic death plaque fade-in (0.1.0.40)
- With automatic plaques enabled, confirm a fatality with delays of 0, 1.5 and 5 seconds. The first visible repaint starts a smooth fade using the configured duration (default 2 seconds) of the frame, title, artwork, portrait, text and controls. Verify both themes, unavailable portrait/artwork and long scrolling inscriptions. Close and Details must not activate until fully visible.
- Destroy a vessel with multiple crew. Each queued plaque must fade independently, including during stock automatic pause; Flight Results must remain held through the delay, fades and acknowledgements. Verify the original stock results restore after the last plaque closes.
- Open a wall plaque manually; it must appear at full opacity immediately. Verify that no tint leaks into Details, other windows or stock GUI, including after closing, disabling automatic plaques, quickload, F2, pause and scene changes.

## Configurable automatic plaque fade (0.1.0.41)
- On new and older saves verify a default fade of 2.0 seconds. Change the fade slider in either settings interface and confirm the same per-save value survives save/reload. Check 0 to 5 seconds in 0.1-second increments.
- Test fade durations 0, 2 and 5 seconds independently of the plaque delay. Zero must display the plaque at full opacity with working buttons on opening. Each queued plaque uses the value at opening and fades independently; changing the value does not restart an active fade. Manual plaques always remain immediate. Verify Flight Results stays deferred until acknowledgement, including during stock automatic pause.


## Automatic death plaque fade-out (0.1.0.43)
- Confirm a death with the fade setting at 2 seconds. After fade-in, click Close and verify the entire plaque fades smoothly to transparent over 2 real-time seconds before it is dismissed. Details closes immediately; repeated clicks must not restart the fade or skip the queue.
- Destroy a vessel with multiple fatalities. Verify each eligible successor fades in while the previous plaque fades out, at the same window position. The original Flight Results remains hidden during the last fade-out and the existing settling interval, including while stock has paused the game.
- Test durations 0 and 5 seconds. Zero disables both animations; five applies to each direction. Verify the saved setting remains shared between the stock difficulty options and Settings / Help. Manual wall plaques open and close immediately.
- Verify quickload, revert, disabled automatic plaques and scene exit during fade-out perform immediate cleanup, and that GUI tint does not leak into other windows.

## Automatic death plaque crossfade (0.1.0.44)
- With fade set to 2 seconds, confirm three or more fatalities. Close the first plaque after fade-in. Verify the outgoing and incoming plaques overlap for the transition, use the same position (including after dragging the first plaque), and alternate distinct window IDs. The next Close and Details must not activate until the transition completes. Repeat through the final plaque; no overlapping window should remain after its final fade-out.
- Confirm a second death shortly before closing the first, with its delay still pending. Verify the first waits for the next deadline before starting the crossfade. A death arriving after the final fade starts may overlap for the remainder of that fade if ready, otherwise displays after its own delay. Verify no skipped or duplicated plaques.
- Test fade duration 0 and 5, both themes, stock automatic pause, repeated Close clicks, quickload, revert, scene exit and disabling automatic plaques mid-transition. Verify both windows are cleaned up, Flight Results stays held until all plaques finish, and manual plaques are unchanged.

## Freestanding editor signs (0.1.0.46)
- With no saved mount overrides, enter the VAB and SPH. Confirm the sign stands in front of the flag wall on two steel posts with feet, not flush against the wall. Orbit the camera and check the rear backing, post attachment and contact with the floor at each building level. Verify the face, live digits and 3:2 aspect ratio are unchanged.
- In Sign Settings, toggle Wall/Posts for the current facility repeatedly; verify immediate movement, no duplicate geometry, no colliders/editor interference and no change to width or clearance. Move both sliders in Posts mode: the sign and support positions should resize and the feet should stay at floor level.
- Set different mounting modes in VAB and SPH, restart KSP and verify both persist. Changing a slider must preserve the mount override and vice versa. Test a legacy override file without postMounted, explicit base config false, custom floorY and legacy heightAboveFloor=-1. Test a failed settings write leaves the previous layout unchanged.
- Switch scenes and reopen the toolbar; confirm only one toolbar controller is registered and that scene cleanup removes only its own controller. Confirm all sign support meshes/materials are removed on scene exit.

## Extended sign supports (0.1.0.47)
- Compare post-mounted signs in VAB and SPH with 0.1.0.46 at identical width, height and floor settings. Both supports must extend 1.5 m farther down, with the feet lowered by the same amount and the upper attachment and board unchanged. Check different heights, detected and configured floor references, and switching back to wall mounting.

## Additional support extension (0.1.0.48)
- Compare post-mounted signs in VAB and SPH with 0.1.0.47 at identical settings. Both posts and their feet must extend 0.25 m farther downward (1.75 m total below the floor reference), with the board, upper attachment and wall mounting unchanged.

## Facility-specific support lengths (0.1.0.49)
- Compare VAB with 0.1.0.48 and SPH with 0.1.0.46 at identical settings. VAB posts and feet must retain the 1.75 m extension; SPH posts and feet must return to the original floor reference. Verify both supports, the board and upper attachments, different height and floor settings, wall mounting and scene changes.

## Outdoor flight signs (0.1.0.50)
- Launch from the pad and runway, then drive or walk to the VAB and SPH. Verify two signs appear beside the main entrance, face outward, retain the mascot and four live digits, and have feet contacting the pavement without blocking the entrance. Check every stock building level and adjust bearing/offset if necessary.
- Verify the sign updates after a casualty and when time advances; compare with both indoor counters. Test quickload, revert, vessel switch, time warp, floating-origin shifts and leaving/returning to KSC. Verify no duplicates or displaced signs.
- Test destruction/repair, returning from orbit, leaving Kerbin, and leaving flight. Verify no orphaned signs, materials or colliders. In the config, disable one sign and adjust the other's dimensions/bearing/offset, restart KSP and check only the intended outdoor sign changes.
- Verify the indoor mounting and placement settings still work, including the 1.75 m VAB-only support extension.

## Outdoor sign corrections (0.1.0.51)
- Verify the VAB sign is anchored to the exact configured `SpaceCenter/ResearchAndDevelopment/Facility/MainBuilding` ID. Check the log for facade raycast versus bounds fallback; confirm the VAB sign is close to the intended front wall, with rear support clearance of 1 m, and adjust bearing/offset if needed. Verify SPH placement independently.
- Walk or drive into the backing, each support post and each foot; verify solid collisions on the scenery physics layer. Confirm indoor signs still have no colliders. Verify cleanup on building destruction, repair, scene changes and floating-origin shifts.
- View each sign from the front and rear in daylight and at night. Both sides must show correctly oriented artwork and the same live number cards, not bare metal or a blank quad. Confirm counter updates and no z-fighting.

## Outdoor board rendering (0.1.0.52)
- Confirm both outdoor boards show artwork and live digits from the front and rear; approach from different distances, in daylight and at night. Check for blank faces and depth flicker. The board is now a single mesh with two artwork faces and four steel edges, rather than overlapping quads. Confirm colliders still work.
- Check KSP.log for the building candidate names, IDs and scores, a warning when no building matches, the artwork texture dimensions and sample colors, shader name, two mesh submeshes and two material slots. If the sign is blank, retain these log lines and note whether the indoor artwork works.

## Configurable outdoor sites (0.1.0.53)
- Verify all eight configured sites load, and the log reports eight configured sites. Confirm exact building IDs, including the two separate R&D buildings and the Astronaut Complex, and check every sign for correct placement, collision and live artwork.
- Disable one site, remove another, add a new child with a distinct name and exact building ID, then restart KSP. Verify the configured site count and that only enabled, matching sites have signs. Test an empty/missing outdoor node (no signs). Check independent overrides and cleanup after scene changes.
- Confirm the indoor VAB/SPH signs and FindBuilding diagnostic logging remain unchanged.

## Outdoor sign live adjustments (0.1.0.54)
- In flight near KSC, open Kerbal Memorial and select Outdoor Signs. Confirm the compact independent window lists all eight configured sites and any additional sites. Disabled/unloaded sites are labeled.
- Adjust X by +5 m, Y by -5 m, and facing from 90 to 180 degrees. Confirm east/north displacement, rotation in place, solid colliders and both posts reaching ground. Check that adjacent signs do not move. Test fractional and negative numeric input and +/- buttons; invalid partial input must not move the sign.
- Save or Close; restart flight and KSP and verify per-site overrides persist. Confirm a sign without a rotation override defaults to its front bearing. Delete override file with KSP closed to restore base values.
- Check the front has live artwork and the rear and four edges are plain steel. Verify face direction at 0/90/180/270 degrees. Confirm indoor signs are unchanged.
- Check F2, pause, queued death plaques, scene changes and rapid numeric edits. Verify there are no duplicate meshes or colliders and no IMGUI errors.


## Indoor/outdoor accident sign settings (0.1.0.57)
- Open Settings / Help: verify both renamed buttons in flight and the indoor button in the editor. Check the matching window titles.
- In flight, open either Outdoor Signs or Outdoor Accident Sign Settings. For each configured site, check X, Y, degrees, width, height above ground and Wall/Posts. Resize one board, verify the aspect ratio and counter, change clearance and check posts and feet follow. Verify other sites stay unchanged.
- Toggle Wall: the board and board collider remain at the same position, with no posts, feet or support colliders. Adjust offsets to align the board with a wall. Toggle Posts and verify both supports and their colliders return.
- Save/Close, reload flight and verify dimensions, mounting, offsets and rotation persist. Old overrides missing the new keys must retain base dimensions and mounting. Open/close with out-of-slider-range base values without changing them; verify they remain unchanged.

## Optional outdoor placement controls (0.1.0.58)
- Open Outdoor Accident Sign Settings from either entry point. Verify X/Y/rotation, their column headings and both introductory labels are hidden; the unchecked Show position and rotation toggle, site names, width, height, mounting and Save/Close remain visible.
- Enable the toggle and verify all placement fields, headings and explanatory text appear. Adjust a site, disable and re-enable the toggle, and verify its values and position are retained. Repeat while scrolling and check for IMGUI layout errors.
- Save and close with the toggle off, reopen and verify it defaults to off. Verify all saved placement values and other settings persist across flight reloads.
