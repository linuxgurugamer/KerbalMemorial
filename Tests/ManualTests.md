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
- Enter the VAB and SPH at each facility level. Verify one sign is centered below the interior flag, with readable three-line text, correct facing, and no clipping or overlap with the wall or flag. Orbit and zoom the camera; the board remains in world space and is occluded by scenery or parts normally.
- Switch between editors, change flag, return to the Space Center and re-enter. Verify no duplicate boards or leftover objects. The sign has no collider and does not interfere with part placement.
- Start a fresh game: sign reads 0 DAYS; advance a day and check 1 DAYS. Trigger a missing or fatal casualty, return to editor, and check 0 DAYS. Advance multiple game days, reload/revert, and verify the sign follows the current timeline. Test with custom calendar day lengths and old saves with only undated casualties (--- DAYS).
- Close the memorial window; sign remains visible. Adjust individual facility offsets in AccidentSign.cfg and restart the game to verify the sign can be fine-tuned without code changes.
- Inspect KSP.log for the accident-sign placement message or a warning if no active flag renderer is available.

- Verify the VAB/SPH safety sign digits render as four hanging green cards, with blank leading cards for values under 1000 and visibly smoother text/edges than the previous release.

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
- Verify the word DAYS is aligned on the same line to the right of all four cards and does not overlap the cards, including for 0, 42, 9999 and unknown-date dashes. Confirm SINCE LAST and ACCIDENT remain on their own lines.

## Smooth sign artwork (0.1.0.13)
- Inspect lettering and printed numerals in both buildings at close range, at normal editor zoom, and at oblique angles. Confirm the 5×7 pixel-block lettering is gone and cards retain their blank leading positions.
- Check 0, 9, 42, 1000, 9999, over-limit counts and unknown-date dashes, including a transition back to 0; old digits must be fully cleared.
- Adjust sign width and height; confirm unchanged 3:2 aspect ratio, no texture compression artifacts, and no new external artwork files needed in GameData. Check at different KSP texture quality settings.

- Open Memorial Wall from the main tabs. Verify original decorative artwork, no fictional plaques, actual portraits/names/dates, missing and respawned labels, unknown data fallback, archive visibility, chronological scrolling with many records, dragging/resizing/centered Close, F2 hiding, and scene cleanup. Verify returned ordinary MIA is excluded and toggling permanent history changes archived plaques.

## Memorial Wall window sizing (0.1.0.15)
- Open the wall with zero casualties and again with several casualties. Without moving the mouse, verify it remains at its intended full size (1060 x 760 on a sufficiently large screen).
- Drag the title or unused background, release the mouse and wait. Verify no shrinking, disappearance or flicker; scroll, resize from the lower-right handle, and verify the new size remains stable. Close and reopen; confirm the chosen size is retained for the current scene.
- Test on smaller resolutions and after changing resolution; the window must stay within the screen. Verify the main window and sign settings still work and click-through is blocked.
