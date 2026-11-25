using System;
using System.Linq;
using ClickThroughFix;
using UnityEngine;

namespace KerbalMemorial
{
    // Independent window and portrait lifetime; does not change the main tab,
    // search/filter, selected record, or full-detail preference.
    internal sealed class PlaqueInfoWindow
    {
        private Rect rect = new Rect(120, 100, 460, 540);
        private Vector2 scroll;
        private string selected, pending, portraitData, portraitId;
        private bool full, open;
        private GUIStyle wrap;
        private Texture2D portrait;
        private CasualtyTracker tracker;
        private Casualty record;

        internal void Open(string id, Rect parent)
        {
            if (string.IsNullOrEmpty(id)) return;
            pending = id;
            if (!open)
            {
                rect.x = parent.x + (parent.width - rect.width) / 2f;
                rect.y = parent.y + 40f;
            }
        }
        internal void Close()
        {
            open = false; selected = pending = null; record = null; tracker = null;
            scroll = Vector2.zero; full = false;
            ReleasePortrait();
        }
        internal void OnGUI(int id, CasualtyTracker current)
        {
            // Start a new GUILayout window only on Layout, not on the mouse-up
            // event that activated the plaque. Keep its contents stable per pass.
            bool bringToFront = false;
            if (Event.current.type == EventType.Layout)
            {
                if (pending != null)
                {
                    if (selected != pending) { scroll = Vector2.zero; full = false; ReleasePortrait(); }
                    selected = pending; pending = null; open = true;
                    bringToFront = true;
                }
                if (open)
                {
                    tracker = current;
                    record = tracker.VisibleRecords().FirstOrDefault(r => r.Id == selected && Ledger.IsMemorialLoss(r));
                }
            }
            if (!open) return;
            if (wrap == null) wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            rect.width = Mathf.Min(460f, Screen.width);
            rect.height = Mathf.Min(540f, Screen.height);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            Rect moved = ClickThruBlocker.GUILayoutWindow(id, rect, Draw, "Kerbal Information", GUILayout.Width(rect.width), GUILayout.Height(rect.height));
            rect.x = moved.x; rect.y = moved.y;
            if (bringToFront && open) GUI.BringWindowToFront(id);
        }
        private void Draw(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            if (record != null) MemorialWindow.DrawDetailContents(tracker, record, full, wrap, DrawPortrait);
            else { ReleasePortrait(); GUILayout.Label("This casualty is no longer present in the memorial history.", wrap); }
            GUILayout.EndScrollView();
            if (record != null && GUILayout.Button(full ? "Hide Full Details" : "Show Full Details"))
            { full = !full; scroll = Vector2.zero; }
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            bool close = GUILayout.Button("Close", GUILayout.Width(100));
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            GUI.DragWindow();
            if (close) Close();
        }
        private void DrawPortrait(Casualty r)
        {
            string data = r.PortraitPng ?? "";
            if (portraitId != r.Id || portraitData != data)
            {
                ReleasePortrait(); portraitId = r.Id; portraitData = data;
                portrait = PortraitCache.Decode(data);
            }
            if (portrait == null) { GUILayout.Label("Portrait unavailable (no valid saved image)", wrap); return; }
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            Rect area = GUILayoutUtility.GetRect(128f, 128f, GUILayout.Width(128), GUILayout.Height(128));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(area, portrait, ScaleMode.ScaleToFit);
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
        }
        private void ReleasePortrait()
        {
            if (portrait != null) UnityEngine.Object.Destroy(portrait);
            portrait = null; portraitId = portraitData = null;
        }
    }
}
