using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClickThroughFix;
using UnityEngine;

namespace KerbalMemorial
{
    // A separate visual memorial. Only the decorative parts of the reference
    // image are embedded; every plaque is drawn from the actual casualty ledger.
    internal sealed class MemorialWallWindow
    {
        private Rect rect = new Rect(60, 55, 1060, 760);
        private Vector2 scroll, resizeDelta;
        private bool resizing, artworkAttempted, refreshRecords;
        private Texture2D header, side, footer, stone, plaque;
        private GUIStyle nameStyle, dateStyle, emptyStyle;
        private List<Casualty> records = new List<Casualty>();
        private readonly Dictionary<string, CachedPortrait> portraits = new Dictionary<string, CachedPortrait>();
        private sealed class CachedPortrait { internal string Data; internal Texture2D Texture; }
        internal bool IsOpen { get; private set; }

        internal void Open(Rect parent)
        {
            if (IsOpen) return;
            rect.x = parent.x + 25f; rect.y = parent.y + 25f;
            scroll = Vector2.zero;
            refreshRecords = true;
            IsOpen = true;
        }
        internal void Close()
        {
            IsOpen = false; resizing = false; resizeDelta = Vector2.zero;
            ClearPortraits();
            Release(ref header); Release(ref side); Release(ref footer); Release(ref stone); Release(ref plaque);
            artworkAttempted = false;
            records.Clear();
        }
        internal void Hide() { resizing = false; }
        private static void Release(ref Texture2D texture)
        { if (texture != null) UnityEngine.Object.Destroy(texture); texture = null; }
        private void ClearPortraits()
        {
            foreach (var p in portraits.Values) Release(ref p.Texture);
            portraits.Clear();
        }
        private static Texture2D Load(string name)
        {
            Texture2D texture = null;
            try
            {
                using (var stream = typeof(MemorialWallWindow).Assembly.GetManifestResourceStream("KerbalMemorial.Artwork." + name + ".png"))
                {
                    if (stream == null) throw new InvalidOperationException("Missing artwork: " + name);
                    using (var data = new MemoryStream())
                    {
                        stream.CopyTo(data);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(data.ToArray(), true)) throw new InvalidOperationException("Invalid artwork: " + name);
                    }
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = name == "WallStone" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                return texture;
            }
            catch (Exception e)
            { Release(ref texture); Debug.LogWarning("[KerbalMemorial] Wall artwork: " + e.Message); return null; }
        }
        private void EnsureArtwork()
        {
            if (artworkAttempted) return;
            artworkAttempted = true;
            header = Load("WallHeader"); side = Load("WallSide"); footer = Load("WallFooter");
            stone = Load("WallStone"); plaque = Load("WallPlaque");
        }
        internal void OnGUI(int id, CasualtyTracker tracker)
        {
            if (!IsOpen || tracker == null) return;
            EnsureArtwork();
            if (refreshRecords || Event.current.type == EventType.Layout)
            {
                records = tracker.VisibleRecords().Where(Ledger.IsMemorialLoss)
                    .OrderBy(r => r.UT < 0 ? double.MaxValue : r.UT).ThenBy(r => r.Name).ToList();
                refreshRecords = false;
            }
            rect.width = Mathf.Clamp(rect.width, Mathf.Min(520, Screen.width), Screen.width);
            rect.height = Mathf.Clamp(rect.height, Mathf.Min(420, Screen.height), Screen.height);
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, Screen.height - rect.height));
            // The wall uses exclusively rect-based GUI controls. GUILayout.Window
            // can shrink to its empty layout group on Layout/Repaint, even when
            // Width/Height options are supplied. Use the fixed-size GUI window
            // and accept only its returned position; the resize handle owns size.
            Rect moved = ClickThruBlocker.GUIWindow(id, rect, Draw, "Kerbal Memorial Wall");
            rect.x = moved.x;
            rect.y = moved.y;
            if (resizeDelta != Vector2.zero)
            {
                rect.width = Mathf.Clamp(rect.width + resizeDelta.x, Mathf.Min(520, Screen.width), Mathf.Max(Mathf.Min(520, Screen.width), Screen.width - rect.x));
                rect.height = Mathf.Clamp(rect.height + resizeDelta.y, Mathf.Min(420, Screen.height), Mathf.Max(Mathf.Min(420, Screen.height), Screen.height - rect.y));
                resizeDelta = Vector2.zero;
            }
        }
        private void Draw(int id)
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15, fontStyle = FontStyle.Bold, richText = false };
                nameStyle.normal.textColor = new Color(1f, .87f, .68f);
                dateStyle = new GUIStyle(nameStyle) { fontSize = 12, fontStyle = FontStyle.Normal };
                emptyStyle = new GUIStyle(nameStyle) { fontSize = 16 };
            }
            GUI.Label(new Rect(10, 24, rect.width - 20, 22), records.Count + " memorial plaques — " + records.Count(r => r.Fatal) + " fatal, " + records.Count(r => !r.Fatal) + " missing");
            DrawWall(new Rect(8, 49, rect.width - 16, rect.height - 91));
            bool close = GUI.Button(new Rect((rect.width - 100) / 2f, rect.height - 34, 100, 26), "Close");
            Resize();
            if (!resizing) GUI.DragWindow();
            if (close) Close();
        }
        private void DrawWall(Rect area)
        {
            if (area.width < 50 || area.height < 50) return;
            if (Event.current.type == EventType.Repaint)
            {
                Tint(area, new Color(.66f, .43f, .26f));
                Tint(new Rect(area.x + 3, area.y + 3, area.width - 6, area.height - 6), new Color(.13f, .115f, .12f));
                if (stone != null) GUI.DrawTextureWithTexCoords(new Rect(area.x + 4, area.y + 4, area.width - 8, area.height - 8), stone, new Rect(0, 0, area.width / 108f, area.height / 61f));
            }
            float sideWidth = Mathf.Min(area.width * .19f, area.height * 239f / 980f);
            Rect sideRect = new Rect(area.x, area.y, sideWidth, area.height);
            if (side != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(sideRect, side, ScaleMode.ScaleToFit);
            float x = area.x + sideWidth + 8f, w = Mathf.Max(1, area.xMax - x - 8f);
            float hh = Mathf.Min(w * 338f / 1099f, area.height * .30f);
            float fh = Mathf.Min(w * 126f / 1099f, area.height * .13f);
            Rect top = new Rect(x, area.y + 4, w, hh);
            Rect bottom = new Rect(x, area.yMax - fh - 4, w, fh);
            if (header != null) { if (Event.current.type == EventType.Repaint) GUI.DrawTexture(top, header, ScaleMode.ScaleToFit); }
            else GUI.Label(top, "KERBAL MEMORIAL WALL\nIN MEMORY OF OUR FALLEN CREW", emptyStyle);
            if (footer != null) { if (Event.current.type == EventType.Repaint) GUI.DrawTexture(bottom, footer, ScaleMode.ScaleToFit); }
            else GUI.Label(bottom, "Their missions endured, and so does their legacy", dateStyle);
            Rect view = new Rect(x, top.yMax + 5f, w, Mathf.Max(1f, bottom.y - top.yMax - 10f));
            DrawPlaques(view);
        }
        private void DrawPlaques(Rect view)
        {
            const float gap = 7f, cardHeight = 104f;
            float width = Mathf.Max(1f, view.width - 22f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (235f + gap)));
            float cardWidth = (width - (columns - 1) * gap) / columns;
            int rows = (records.Count + columns - 1) / columns;
            float contentHeight = Mathf.Max(view.height, rows * (cardHeight + gap) + gap);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, width, contentHeight), false, true);
            var used = new HashSet<string>();
            bool paint = Event.current.type == EventType.Repaint;
            if (records.Count == 0) GUI.Label(new Rect(0, 0, width, view.height), "No lost Kerbals recorded.", emptyStyle);
            // Only decode and draw portraits that are currently in view.
            if (paint)
            {
                int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / (cardHeight + gap)));
                int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scroll.y + view.height) / (cardHeight + gap)));
                for (int row = firstRow; row <= lastRow; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index >= records.Count) break;
                        var r = records[index]; used.Add(r.Id);
                        DrawPlaque(new Rect(col * (cardWidth + gap), gap + row * (cardHeight + gap), cardWidth, cardHeight), r);
                    }
            }
            GUI.EndScrollView();
            if (paint)
                foreach (string key in portraits.Keys.Where(k => !used.Contains(k)).ToArray())
                { var entry = portraits[key]; Release(ref entry.Texture); portraits.Remove(key); }
        }
        private void DrawPlaque(Rect card, Casualty r)
        {
            if (plaque != null) GUI.DrawTexture(card, plaque, ScaleMode.StretchToFill);
            else Tint(card, new Color(.30f, .20f, .13f));
            float portraitWidth = Mathf.Min(79f, card.width * .33f);
            Rect picture = new Rect(card.x + 12f, card.y + 12f, portraitWidth, card.height - 24f);
            Tint(picture, new Color(.15f, .10f, .075f));
            var texture = GetPortrait(r);
            if (texture != null) GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit);
            else GUI.Label(picture, "No\nportrait", dateStyle);
            float tx = picture.xMax + 5f, tw = Mathf.Max(1f, card.xMax - tx - 10f);
            GUI.Label(new Rect(tx, card.y + 8f, tw, 39f), r.Name, nameStyle);
            Tint(new Rect(tx + 4f, card.y + 48f, Mathf.Max(1f, tw - 8f), 1f), new Color(.76f, .52f, .30f));
            double ut = r.Fatal && r.DeathUT >= 0 ? r.DeathUT : r.UT;
            string date = ut < 0 || double.IsNaN(ut) || double.IsInfinity(ut) ? "Date unknown" : KSPUtil.PrintDateCompact(ut, false);
            GUI.Label(new Rect(tx, card.y + 50f, tw, 24f), date, dateStyle);
            var tracker = CasualtyTracker.Instance;
            string status = r.Fatal ? (r.Closed ? "Respawned" : "Fallen") : "Missing";
            if (tracker != null && !tracker.IsLive(r)) status += " • Archived";
            GUI.Label(new Rect(tx, card.y + 74f, tw, 23f), status, dateStyle);
        }
        private Texture2D GetPortrait(Casualty r)
        {
            string data = r.PortraitPng ?? "";
            CachedPortrait p;
            if (!portraits.TryGetValue(r.Id, out p)) { p = new CachedPortrait(); portraits.Add(r.Id, p); }
            if (p.Data != data) { Release(ref p.Texture); p.Data = data; p.Texture = PortraitCache.Decode(data); }
            return p.Texture;
        }
        private static void Tint(Rect area, Color color)
        {
            Color old = GUI.color;
            GUI.color = color; GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = old;
        }
        private void Resize()
        {
            var handle = new Rect(rect.width - 21, rect.height - 21, 20, 20);
            GUI.Label(handle, "//"); var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition)) { resizing = true; e.Use(); }
            if (e.type == EventType.MouseDrag && resizing) { resizeDelta += e.delta; e.Use(); }
            if (e.rawType == EventType.MouseUp) resizing = false;
        }
    }
}
