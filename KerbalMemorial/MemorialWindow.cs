using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using ToolbarControl_NS;
using ClickThroughFix;

namespace KerbalMemorial
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class RegisterToolbar : MonoBehaviour
    {
        public void Start() { ToolbarControl.RegisterMod("KerbalMemorial", "Kerbal Memorial"); }
    }
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public sealed class MemorialWindow : MonoBehaviour
    {
        private static ToolbarControl toolbarController;
        private bool open, openMainRequested, hidden, paused, subscribed, resizing, showFullDetails;
        private readonly SignSettingsWindow signSettings = new SignSettingsWindow();
        private readonly MemorialWallWindow wall = new MemorialWallWindow();
        private Rect rect = new Rect(100, 80, 780, 580);
        private static Rect lastRect = new Rect(100, 80, 780, 580);
        private Vector2 listScroll, detailScroll, statsScroll, settingsScroll, resizeDelta;
        private int tab, filter;
        private string search = "", selected = "", message = "";
        private GUIStyle wrap, incidentStyle;
        private Texture2D detailPortrait;
        private string portraitRecord = "", portraitData = "";
        private static readonly string[] Filters = { "All", "Fatal", "Missing", "Respawned", "Returned", "Archived" };
        public void Start()
        {
            if (toolbarController != null ||
                (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor && HighLogic.LoadedScene != GameScenes.SPACECENTER && HighLogic.LoadedScene != GameScenes.TRACKSTATION))
                return;
            rect = lastRect;
            toolbarController = gameObject.AddComponent<ToolbarControl>();
            toolbarController.AddToAllToolbars(wall.Open, CloseAll,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.TRACKSTATION | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                "KerbalMemorial", "memorial", "KerbalMemorial/PluginData/Textures/icon38", "KerbalMemorial/PluginData/Textures/icon24", "Kerbal Memorial");
            GameEvents.onHideUI.Add(Hide); GameEvents.onShowUI.Add(Show);
            GameEvents.onGamePause.Add(OnPause); GameEvents.onGameUnpause.Add(OnUnpause);
            subscribed = true;
        }
        private void CancelResize() { resizing = false; resizeDelta = Vector2.zero; wall.Hide(); }
        private void Hide() { hidden = true; CancelResize(); }
        private void Show() { hidden = false; }
        // Pause and F2 are independent: resuming one must not clear the other.
        // Keep all open/selection/scroll state intact while drawing is suspended.
        private void OnPause() { paused = true; CancelResize(); }
        private void OnUnpause() { paused = false; }
        public void OnDestroy()
        {
            CloseAll();
            if (subscribed)
            {
                GameEvents.onHideUI.Remove(Hide); GameEvents.onShowUI.Remove(Show);
                GameEvents.onGamePause.Remove(OnPause); GameEvents.onGameUnpause.Remove(OnUnpause);
                subscribed = false;
            }
            if (toolbarController != null) { toolbarController.OnDestroy(); Destroy(toolbarController); }
        }
        // The wall is the toolbar's root window. The main ledger is optional.
        private void RequestMainWindow() { openMainRequested = true; }
        private void CloseMain()
        {
            open = false; openMainRequested = false; resizing = false; resizeDelta = Vector2.zero;
            signSettings.Close(); ReleasePortrait();
        }
        private void CloseAll()
        {
            CloseMain(); wall.Close();
        }
        public void OnGUI()
        {
            // All child windows are drawn through this method. Check the live
            // flight pause state too, in case the addon starts after a pause event.
            if (paused || (HighLogic.LoadedSceneIsFlight && FlightDriver.Pause))
            {
                CancelResize();
                return;
            }
            if (!wall.IsOpen || hidden || CasualtyTracker.Instance == null) return;
            GUI.skin = HighLogic.Skin;
            // Open the GUILayout-based ledger on a Layout event, not in the
            // wall's MouseUp callback, so its first repaint has valid layout data.
            bool bringMainToFront = openMainRequested && Event.current.type == EventType.Layout;
            if (bringMainToFront) { openMainRequested = false; open = true; }
            wall.OnGUI(GetInstanceID() ^ 0x4b4d5700, CasualtyTracker.Instance, RequestMainWindow);
            if (!wall.IsOpen)
            {
                CloseMain();
                if (toolbarController != null) toolbarController.SetFalse(false);
                return;
            }
            if (!open) return;
            if (wrap == null) wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (incidentStyle == null) incidentStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, wordWrap = true };
            rect.width = Mathf.Clamp(rect.width, Mathf.Min(550, Screen.width), Screen.width);
            rect.height = Mathf.Clamp(rect.height, Mathf.Min(420, Screen.height), Screen.height);
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, Screen.height - rect.height));
            rect = ClickThruBlocker.GUILayoutWindow(GetInstanceID(), rect, Draw, "Kerbal Memorial", GUILayout.Width(rect.width), GUILayout.Height(rect.height));
            if (resizeDelta != Vector2.zero)
            {
                rect.width = Mathf.Clamp(rect.width + resizeDelta.x, Mathf.Min(550, Screen.width), Mathf.Max(Mathf.Min(550, Screen.width), Screen.width - rect.x));
                rect.height = Mathf.Clamp(rect.height + resizeDelta.y, Mathf.Min(420, Screen.height), Mathf.Max(Mathf.Min(420, Screen.height), Screen.height - rect.y));
                resizeDelta = Vector2.zero;
            }
            lastRect = rect;
            if (open && bringMainToFront) GUI.BringWindowToFront(GetInstanceID());
            if (open) signSettings.OnGUI(GetInstanceID() ^ 0x4b4d5300);
        }
        private void Draw(int id)
        {
            var tracker = CasualtyTracker.Instance;
            if (tracker == null) return;
            var records = tracker.VisibleRecords();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Incidents: " + records.Count + "   Fatalities: " + records.Count(r => r.Fatal) + "\nMissing: " + records.Count(r => !r.Fatal && !r.Closed) + "   Respawned: " + records.Count(r => r.Fatal && r.Closed), wrap, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Time since last incident:\n" + IncidentElapsed(tracker), incidentStyle, GUILayout.Width(Mathf.Min(240f, rect.width * .43f)));
            GUILayout.EndHorizontal();
            tab = GUILayout.Toolbar(tab, new[] { "Memorial", "Statistics", "Settings / Help" });
            if (tab == 0) DrawMemorial(tracker, records);
            else if (tab == 1) DrawStats(tracker, records);
            else DrawSettings(tracker);
            if (!string.IsNullOrEmpty(tracker.Error)) GUILayout.Label(tracker.Error, wrap);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, wrap);
            GUILayout.BeginHorizontal();
            // Equal-width side groups keep Close centered regardless of the other controls.
            float footerSide = Mathf.Min(180f, rect.width * .31f);
            GUILayout.BeginHorizontal(GUILayout.Width(footerSide));
            if (GUILayout.Button("Export CSV", GUILayout.Width(120)))
            {
                try { message = "Exported: " + Persistence.Export(records, new HashSet<string>(tracker.Live.Records.Select(r => r.Id))); }
                catch (Exception e) { message = "Export failed: " + e.Message; }
            }
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(100))) CloseMain();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(GUILayout.Width(footerSide));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Sign Settings", GUILayout.Width(112))) signSettings.Open(rect);
            GUILayout.Space(22);
            GUILayout.EndHorizontal(); GUILayout.EndHorizontal();
            // Let interactive controls and the resize handle consume input first.
            Resize();
            if (open && !resizing) GUI.DragWindow();
            if (!open || tab != 0) ReleasePortrait();
        }
        private void Resize()
        {
            var handle = new Rect(rect.width - 21, rect.height - 21, 20, 20);
            GUI.Label(handle, "//"); var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition)) { resizing = true; e.Use(); }
            if (e.type == EventType.MouseDrag && resizing)
            { resizeDelta += e.delta; e.Use(); }
            if (e.rawType == EventType.MouseUp) resizing = false;
        }
        private void DrawMemorial(CasualtyTracker tracker, List<Casualty> records)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label("Search", GUILayout.Width(52)); search = GUILayout.TextField(search); if (GUILayout.Button("Clear", GUILayout.Width(55))) search = ""; GUILayout.EndHorizontal();
            filter = GUILayout.Toolbar(filter, Filters);
            var filtered = records.Where(r => Matches(tracker, r)).ToList();
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUILayout.Width(rect.width * .46f));
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            foreach (var r in filtered)
            {
                string label = r.Name + " — " + r.DisplayStatus + (tracker.IsLive(r) ? "" : " [Archived]") + "\n" + Date(r.UT) + " | " + (r.Location == null ? "Unknown vessel" : Value(r.Location.Vessel));
                if (GUILayout.Toggle(selected == r.Id, label, GUI.skin.button)) selected = r.Id;
            }
            if (filtered.Count == 0) GUILayout.Label("No matching casualties.");
            GUILayout.EndScrollView(); GUILayout.Label(filtered.Count + " / " + records.Count + " incidents"); GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            detailScroll = GUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));
            var detail = filtered.FirstOrDefault(r => r.Id == selected) ?? filtered.FirstOrDefault();
            if (detail != null) { selected = detail.Id; DrawDetail(tracker, detail); }
            else { ReleasePortrait(); GUILayout.Label("Select a casualty to view details."); }
            GUILayout.EndScrollView();
            // Keep the toggle outside the scrolling details, always accessible.
            // Change the layout only after drawing the current details.
            if (detail != null && GUILayout.Button(showFullDetails ? "Hide Full Details" : "Show Full Details"))
            {
                showFullDetails = !showFullDetails;
                detailScroll = Vector2.zero;
            }
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }
        private bool Matches(CasualtyTracker t, Casualty r)
        {
            if (filter == 1 && !r.Fatal || filter == 2 && (r.Fatal || r.Closed) || filter == 3 && !(r.Fatal && r.Closed) || filter == 4 && !(!r.Fatal && r.Closed) || filter == 5 && t.IsLive(r)) return false;
            var c = r.Location ?? new Context();
            return string.Join(" ", r.Name, r.OriginalName, r.Trait, r.Cause, r.DisplayStatus, c.Vessel, c.Body, r.Notes).IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private void DrawDetail(CasualtyTracker tracker, Casualty r)
        {
            DrawDetailContents(tracker, r, showFullDetails, wrap, DrawPortrait);
        }
        // Shared by the main panel and the independently opened plaque information window.
        internal static void DrawDetailContents(CasualtyTracker tracker, Casualty r, bool full, GUIStyle wrap, Action<Casualty> drawPortrait)
        {
            Action<string, string> Field = (key, val) => GUILayout.Label(key + ": " + Value(val), wrap);
            var c = r.Location ?? new Context();
            GUILayout.Label(r.Name + " — " + r.DisplayStatus, wrap);
            drawPortrait(r);
            // The summary retains the portrait and only the requested text fields.
            Field("Date fatality confirmed", Date(r.DeathUT));
            Field("Cause", r.Cause);
            Field("Evidence", r.Source);
            Field("Vessel name", c.Vessel);
            Field("Part", string.IsNullOrEmpty(c.PartTitle) ? c.Part : c.PartTitle);
            Field("Body", c.Body);
            if (!full) return;

            GUILayout.Space(8);
            GUILayout.Label("Additional details", wrap);
            if (r.Name != r.OriginalName) Field("Original name", r.OriginalName);
            Field("Timeline", tracker.IsLive(r) ? "Current save" : "Archived / alternate timeline");
            Field("Profession", r.Trait + " / " + r.CrewType); Field("Gender / level", r.Gender + " / " + r.Level);
            Field("Incident", Date(r.UT)); if (r.Closed) Field("Returned", Date(r.ReturnUT));
            Field("Cause confidence", r.Confidence);
            Field("Vessel ID", c.VesselId); Field("Situation", c.Situation); Field("EVA", c.EVA);
            Field("Latitude / longitude", Metric(c.Latitude, "°") + " / " + Metric(c.Longitude, "°"));
            Field("Altitude", Metric(c.Altitude, " m")); Field("Surface speed", Metric(c.Speed, " m/s")); Field("G-force", Metric(c.Gee, " g")); Field("Mission elapsed time", Metric(c.MET, " s"));
            Field("Context", Value(c.Quality) + " — " + Date(c.UT));
            GUILayout.Label("Notes"); var note = GUILayout.TextArea(r.Notes ?? "", GUILayout.MinHeight(65)); if (note != r.Notes) tracker.EditNote(r, note);
            if (!string.IsNullOrEmpty(r.History)) { GUILayout.Label("Career / flight history"); GUILayout.Label(r.History, wrap); }
        }
        private void ReleasePortrait()
        {
            if (detailPortrait != null) Destroy(detailPortrait);
            detailPortrait = null; portraitRecord = portraitData = "";
        }
        private void DrawPortrait(Casualty r)
        {
            if (portraitRecord != r.Id || portraitData != r.PortraitPng)
            {
                ReleasePortrait(); portraitRecord = r.Id; portraitData = r.PortraitPng ?? "";
                detailPortrait = PortraitCache.Decode(portraitData);
            }
            if (detailPortrait != null)
            {
                GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
                Rect area = GUILayoutUtility.GetRect(128f, 128f, GUILayout.Width(128), GUILayout.Height(128));
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(area, detailPortrait, ScaleMode.ScaleToFit);
                GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Portrait unavailable (no valid saved image)", wrap);
        }
        private static string Value(string s) => string.IsNullOrEmpty(s) ? "Unknown" : s;
        private static string Metric(double n, string unit) => double.IsNaN(n) || double.IsInfinity(n) ? "Unknown" : n.ToString("N2") + unit;
        private static string Date(double ut) => ut < 0 ? "Unknown" : KSPUtil.PrintDateCompact(ut, true);
        private static string IncidentElapsed(CasualtyTracker tracker)
        {
            double now = CasualtyTracker.Now;
            double last = Ledger.LatestIncidentUT(tracker.Live.Records, now);
            if (last < 0) return tracker.Live.Records.Count == 0 ? "None recorded" : "Unknown";
            return Ledger.FormatElapsedDays(now - last, KSPUtil.dateTimeFormatter.Day);
        }
        private void DrawStats(CasualtyTracker t, List<Casualty> records)
        {
            statsScroll = GUILayout.BeginScrollView(statsScroll, GUILayout.ExpandHeight(true));
            var deaths = records.Where(r => r.Fatal).ToList();
            GUILayout.Label("Fatal incidents: " + deaths.Count + "   Distinct names: " + deaths.Select(r => r.Name).Distinct().Count());
            GUILayout.Label("Current timeline fatalities: " + deaths.Count(t.IsLive) + "   Archived: " + deaths.Count(r => !t.IsLive(r)));
            var recent = deaths.Where(r => t.IsLive(r) && r.DeathUT >= 0 && r.DeathUT <= CasualtyTracker.Now).Select(r => r.DeathUT).DefaultIfEmpty(-1).Max();
            GUILayout.Label("Time since last recorded current-timeline fatality: " + (recent < 0 ? "Unknown" : Ledger.FormatElapsedDays(CasualtyTracker.Now - recent, KSPUtil.dateTimeFormatter.Day)));
            Group("By celestial body", deaths, r => r.Location == null ? "Unknown" : Value(r.Location.Body));
            Group("By cause", deaths, r => r.Cause); Group("By profession", deaths, r => Value(r.Trait));
            Group("By vessel", deaths, r => r.Location == null ? "Unknown" : Value(r.Location.Vessel));
            GUILayout.EndScrollView();
        }
        private static void Group(string title, List<Casualty> records, Func<Casualty,string> key)
        {
            GUILayout.Space(10); GUILayout.Label(title);
            foreach (var g in records.GroupBy(key).OrderByDescending(x => x.Count()).ThenBy(x => x.Key))
            {
                GUILayout.BeginHorizontal(); GUILayout.Label(g.Key, GUILayout.Width(210));
                GUILayout.Label(new string('■', Mathf.Max(1, Mathf.RoundToInt(24f * g.Count() / Math.Max(1, records.Count)))), GUILayout.ExpandWidth(true));
                GUILayout.Label(g.Count().ToString(), GUILayout.Width(45)); GUILayout.EndHorizontal();
            }
        }
        private void DrawSettings(CasualtyTracker t)
        {
            settingsScroll = GUILayout.BeginScrollView(settingsScroll, GUILayout.ExpandHeight(true));
            GUILayout.Label("Settings are saved in the game difficulty options. Permanent mode additionally writes an archive under this save folder.", wrap);
            var s = CasualtyTracker.Options;
            s.permanentHistory = GUILayout.Toggle(s.permanentHistory, "Keep incidents across quickloads and reverts");
            s.notifications = GUILayout.Toggle(s.notifications, "Show casualty notifications");
            s.importExisting = GUILayout.Toggle(s.importExisting, "Import previously dead or missing Kerbals");
            GUILayout.Space(8);
            GUILayout.Label("Memorial Wall Theme", wrap);
            s.alternateWallTheme = GUILayout.Toolbar(s.alternateWallTheme ? 1 : 0, new[] { "Classic", "Alternate" }) == 1;
            s.hideWallThemeButton = GUILayout.Toggle(s.hideWallThemeButton, "Hide theme button on memorial wall");
            GUILayout.Space(10);
            GUILayout.Label("Missing is not automatically a fatality. An explicit crew-killed event or Dead status confirms a death. A confirmed fatality remains counted if a Kerbal respawns. Ordinary returns from MIA are not fatalities. Archived incidents do not alter gameplay. Context and inferred causes are best effort; unknown values are not fabricated.", wrap);
            GUILayout.Label("CSV exports all currently visible history (not just the search results).", wrap);
            if (GUILayout.Button("Accident Sign Settings", GUILayout.Width(190))) signSettings.Open(rect);
            GUILayout.EndScrollView();
        }
    }
}
