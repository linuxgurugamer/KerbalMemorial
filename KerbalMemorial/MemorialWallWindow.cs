using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClickThroughFix;
using UnityEngine;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    // A separate visual memorial. Only the decorative parts of the reference
    // image are embedded; every plaque is drawn from the actual casualty ledger.
    internal sealed class MemorialWallWindow
    {
        private Rect rect = new Rect(60, 55, 1060, 760);
        private Vector2 scroll, resizeDelta;
        private bool resizing, artworkAttempted, refreshRecords, useAlternateTheme, hideThemeButton;
        private readonly IndividualPlaqueWindow individual = new IndividualPlaqueWindow();
        private Texture2D header, side, footer, plaque, wallBackground;
        private Texture2D altFull, altPlaque;
        private int wallBackgroundWidth = -1, wallBackgroundHeight = -1;
        private GUIStyle nameStyle, dateStyle, emptyStyle;
        private GUIStyle alternateNameStyle, alternateDateStyle, alternateStatusStyle, alternateEmptyStyle;
        private List<Casualty> records = new List<Casualty>();
        private readonly Dictionary<string, CachedPortrait> portraits = new Dictionary<string, CachedPortrait>();
        private sealed class CachedPortrait
        {
            internal string Data;
            internal Texture2D Texture;
        }
        internal bool IsOpen
        {
            get;
            private set;
        }

        internal void Open()
        {
            if (IsOpen)
                return;
            scroll = Vector2.zero;
            refreshRecords = true;
            IsOpen = true;
        }
        internal void Close()
        {
            IsOpen = false;
            resizing = false;
            resizeDelta = Vector2.zero;
            individual.Close();
            ClearPortraits();
            Release(ref header);
            Release(ref side);
            Release(ref footer);
            Release(ref plaque);
            Release(ref wallBackground);
            Release(ref altFull);
            Release(ref altPlaque);
            wallBackgroundWidth = wallBackgroundHeight = -1;
            artworkAttempted = false;
            records.Clear();
        }
        internal void Hide()
        {
            resizing = false;
            resizeDelta = Vector2.zero;
        }
        private static void Release(ref Texture2D texture)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            texture = null;
        }
        private void ClearPortraits()
        {
            foreach (var p in portraits.Values)
                Release(ref p.Texture);
            portraits.Clear();
        }
        private static Texture2D Load(string name)
        {
            Texture2D texture = null;
            try
            {
                using (var stream = typeof(MemorialWallWindow).Assembly.GetManifestResourceStream("KerbalMemorial.Artwork." + name + ".png"))
                {
                    if (stream == null)
                        throw new InvalidOperationException("Missing artwork: " + name);
                    using (var data = new MemoryStream())
                    {
                        stream.CopyTo(data);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(data.ToArray(), true))
                            throw new InvalidOperationException("Invalid artwork: " + name);
                    }
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
            catch (Exception e)
            {
                Release(ref texture);
                Log.Warning("[KerbalMemorial] Wall artwork: " + e.Message);
                return null;
            }
        }
        private void EnsureArtwork()
        {
            if (artworkAttempted)
                return;
            artworkAttempted = true;
            header = Load("WallHeader");
            side = Load("WallSide");
            footer = Load("WallFooter");
            plaque = Load("WallPlaque");
            altFull = Load("AltWallFull");
            altPlaque = Load("AltWallPlaque");
        }
        internal void OnGUI(int id, CasualtyTracker tracker, Action openMemorial, Action closeWall)
        {
            if (!IsOpen || tracker == null)
                return;
            EnsureArtwork();
            var settings = CasualtyTracker.Options;
            if (settings.alternateWallTheme != useAlternateTheme)
            {
                useAlternateTheme = settings.alternateWallTheme;
                scroll = Vector2.zero;
            }
            hideThemeButton = settings.hideWallThemeButton;
            if (refreshRecords || Event.current.type == EventType.Layout)
            {
                records = tracker.VisibleRecords(refreshRecords).Where(Ledger.IsMemorialLoss)
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
            Rect moved = ClickThruBlocker.GUIWindow(id, rect, windowId => Draw(windowId, openMemorial, closeWall), "Kerbal Memorial Wall");
            rect.x = moved.x;
            rect.y = moved.y;
            if (resizeDelta != Vector2.zero)
            {
                rect.width = Mathf.Clamp(rect.width + resizeDelta.x, Mathf.Min(520, Screen.width), Mathf.Max(Mathf.Min(520, Screen.width), Screen.width - rect.x));
                rect.height = Mathf.Clamp(rect.height + resizeDelta.y, Mathf.Min(420, Screen.height), Mathf.Max(Mathf.Min(420, Screen.height), Screen.height - rect.y));
                resizeDelta = Vector2.zero;
            }
            if (IsOpen)
                individual.OnGUI(id ^ 0x0000504c, tracker);
        }
        private void Draw(int id, Action openMemorial, Action closeWall)
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15, fontStyle = FontStyle.Bold, richText = false };
                nameStyle.normal.textColor = new Color(1f, .87f, .68f);
                dateStyle = new GUIStyle(nameStyle) { fontSize = 12, fontStyle = FontStyle.Normal };
                emptyStyle = new GUIStyle(nameStyle) { fontSize = 16 };
                alternateNameStyle = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleLeft, fontSize = 15, clipping = TextClipping.Clip };
                alternateNameStyle.normal.textColor = new Color(.96f, .97f, .98f);
                alternateDateStyle = new GUIStyle(alternateNameStyle) { fontSize = 12, fontStyle = FontStyle.Normal, wordWrap = false };
                alternateStatusStyle = new GUIStyle(alternateDateStyle) { fontSize = 10, wordWrap = true };
                alternateStatusStyle.normal.textColor = new Color(.81f, .84f, .87f);
                alternateEmptyStyle = new GUIStyle(alternateDateStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            string themeCaption = useAlternateTheme ? "Theme: Alternate" : "Theme: Classic";
            if (!hideThemeButton && GUI.Button(new Rect(rect.width - 150f, 24f, 132f, 24f), themeCaption))
                CasualtyTracker.Options.alternateWallTheme = !useAlternateTheme;
            GUI.Label(new Rect(10, 24, rect.width - (hideThemeButton ? 20 : 170), 22), records.Count + " memorial plaques — " + records.Count(r => r.Fatal) + " fatal, " + records.Count(r => !r.Fatal) + " missing");
            DrawWall(new Rect(8, 49, rect.width - 16, rect.height - 91));
            if (GUI.Button(new Rect(10, rect.height - 34, Mathf.Min(172f, (rect.width - 100f) / 2f - 20f), 26), "Kerbal Memorial"))
                openMemorial();
            bool close = GUI.Button(new Rect((rect.width - 100) / 2f, rect.height - 34, 100, 26), "Close");
            Resize();
            if (!resizing)
                GUI.DragWindow();
            if (close)
                closeWall();
        }
        private void DrawWall(Rect area)
        {
            if (area.width < 50 || area.height < 50)
                return;
            if (useAlternateTheme)
                DrawAlternateWall(area);
            else
                DrawClassicWall(area);
        }

        private void DrawClassicWall(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Tint(area, new Color(.66f, .43f, .26f));
                Rect inner = new Rect(area.x + 3, area.y + 3, area.width - 6, area.height - 6);
                Tint(inner, new Color(.13f, .115f, .12f));
                DrawBackground(new Rect(area.x + 4, area.y + 4, area.width - 8, area.height - 8));
            }
            float sideWidth = Mathf.Min(area.width * .19f, area.height * 239f / 980f);
            Rect sideRect = new Rect(area.x, area.y, sideWidth, area.height);
            if (side != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(sideRect, side, ScaleMode.ScaleToFit);
            float x = area.x + sideWidth + 8f, w = Mathf.Max(1, area.xMax - x - 8f);
            float hh = Mathf.Min(w * 338f / 1099f, area.height * .30f);
            float fh = Mathf.Min(w * 126f / 1099f, area.height * .13f);
            Rect top = new Rect(x, area.y + 4, w, hh);
            Rect bottom = new Rect(x, area.yMax - fh - 4, w, fh);
            if (header != null)
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(top, header, ScaleMode.ScaleToFit);
            }
            else
                GUI.Label(top, "KERBAL MEMORIAL WALL\nIN MEMORY OF OUR FALLEN CREW", emptyStyle);
            if (footer != null)
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(bottom, footer, ScaleMode.ScaleToFit);
            }
            else
                GUI.Label(bottom, "Their missions endured, and so does their legacy", dateStyle);
            Rect view = new Rect(x, top.yMax + 5f, w, Mathf.Max(1f, bottom.y - top.yMax - 10f));
            DrawPlaques(view, plaque, 7f, 104f, false);
        }

        private void DrawAlternateWall(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Tint(area, new Color(.34f, .34f, .36f));
                if (altFull != null)
                    GUI.DrawTexture(area, altFull, ScaleMode.StretchToFill);
                else
                    Tint(area, new Color(.19f, .19f, .21f));
            }

            // AltWallFull is a cleaned, opaque reference frame. There are no
            // sample plaques, names, dates or portraits beneath this scroll view.
            Rect view = new Rect(
                area.x + area.width * 0.028f,
                area.y + area.height * 0.288f,
                area.width * 0.944f,
                area.height * 0.61f);
            DrawPlaques(view, altPlaque, 10f, 111f, true);
        }

        private void DrawBackground(Rect area)
        {
            int targetWidth = Mathf.Clamp(Mathf.RoundToInt(area.width), 256, 1536);
            int targetHeight = Mathf.Clamp(Mathf.RoundToInt(area.height), 256, 1536);
            if (wallBackground == null || Mathf.Abs(targetWidth - wallBackgroundWidth) > 32 || Mathf.Abs(targetHeight - wallBackgroundHeight) > 32)
            {
                Release(ref wallBackground);
                wallBackground = BuildBackground(targetWidth, targetHeight);
                wallBackgroundWidth = targetWidth;
                wallBackgroundHeight = targetHeight;
            }
            if (wallBackground != null)
                GUI.DrawTexture(area, wallBackground, ScaleMode.StretchToFill);
            else
                Tint(area, new Color(.24f, .22f, .21f));
        }

        private static Texture2D BuildBackground(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.name = "KerbalMemorial Wall Background";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 8;

            Color dark = new Color(.18f, .17f, .17f, 1f);
            Color mid = new Color(.28f, .27f, .265f, 1f);
            Color light = new Color(.39f, .37f, .35f, 1f);
            Color warm = new Color(.47f, .435f, .39f, 1f);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; ++y)
            {
                float v = height > 1 ? y / (float)(height - 1) : 0f;
                for (int x = 0; x < width; ++x)
                {
                    float u = width > 1 ? x / (float)(width - 1) : 0f;
                    float n1 = Mathf.PerlinNoise(7.3f + u * 3.8f, 19.1f + v * 4.2f);
                    float n2 = Mathf.PerlinNoise(31.7f + u * 10.7f, 9.4f + v * 11.9f);
                    float n3 = Mathf.PerlinNoise(3.1f + u * 24.0f, 5.2f + v * 23.0f);
                    float n4 = Mathf.PerlinNoise(17.4f + u * 52.0f, 23.9f + v * 54.0f);
                    float tone = Mathf.Clamp01(0.18f + n1 * 0.50f + (n2 - 0.5f) * 0.22f);
                    Color color = Color.Lerp(dark, mid, tone);
                    color = Color.Lerp(color, light, Mathf.Clamp01((n3 - 0.25f) * 0.45f));

                    float crackWave = Mathf.Abs(Mathf.Sin((u * 18.0f) + (v * 4.8f) + n2 * 4.3f));
                    float crack = 1f - crackWave;
                    crack = Mathf.SmoothStep(0.82f, 0.98f, crack);
                    if (crack > 0f)
                        color = Color.Lerp(color, warm, crack * 0.28f);

                    float speck = Mathf.Clamp01((n4 - 0.78f) / 0.22f);
                    if (speck > 0f)
                        color = Color.Lerp(color, light, speck * 0.22f);

                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float vignette = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 1.18f);
                    color *= 0.86f + vignette * 0.14f;
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private void DrawPlaques(Rect view, Texture2D plaqueTexture, float gap, float cardHeight, bool alternate)
        {
            float width = Mathf.Max(1f, view.width - 22f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (235f + gap)));
            float cardWidth = (width - (columns - 1) * gap) / columns;
            int rows = (records.Count + columns - 1) / columns;
            float contentHeight = Mathf.Max(view.height, rows * (cardHeight + gap) + gap);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, width, contentHeight), false, true);
            var used = new HashSet<string>();
            bool paint = Event.current.type == EventType.Repaint;
            if (records.Count == 0)
                GUI.Label(new Rect(0, 0, width, view.height), "No lost Kerbals recorded.", alternate ? alternateEmptyStyle : emptyStyle);
            // Buttons must participate in input events as well as repaint. Keep
            // the same scroll-view geometry for both themes, and decode portraits
            // only during repaint. GUI.Button consumes mouse input before DragWindow.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / (cardHeight + gap)));
            int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scroll.y + view.height) / (cardHeight + gap)));
            for (int row = firstRow; row <= lastRow; row++)
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= records.Count)
                        break;
                    var r = records[index];
                    Rect card = new Rect(col * (cardWidth + gap), gap + row * (cardHeight + gap), cardWidth, cardHeight);
                    if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                        individual.Open(r.Id, rect);
                    if (!paint)
                        continue;
                    used.Add(r.Id);
                    if (alternate)
                        DrawAlternatePlaque(card, r, plaqueTexture);
                    else
                        DrawPlaque(card, r, plaqueTexture);
                    if (card.Contains(Event.current.mousePosition))
                    {
                        Color border = alternate ? new Color(.80f, .85f, .90f, .9f) : new Color(.95f, .73f, .44f, .9f);
                        Tint(new Rect(card.x, card.y, card.width, 1), border);
                        Tint(new Rect(card.x, card.yMax - 1, card.width, 1), border);
                        Tint(new Rect(card.x, card.y, 1, card.height), border);
                        Tint(new Rect(card.xMax - 1, card.y, 1, card.height), border);
                    }
                }
            GUI.EndScrollView();
            if (paint)
                foreach (string key in portraits.Keys.Where(k => !used.Contains(k)).ToArray())
                {
                    var entry = portraits[key];
                    Release(ref entry.Texture);
                    portraits.Remove(key);
                }
        }
        private void DrawPlaque(Rect card, Casualty r, Texture2D plaqueTexture)
        {
            if (plaqueTexture != null)
                GUI.DrawTexture(card, plaqueTexture, ScaleMode.StretchToFill);
            else
                Tint(card, new Color(.30f, .20f, .13f));
            float portraitWidth = Mathf.Min(79f, card.width * .33f);
            Rect picture = new Rect(card.x + 12f, card.y + 12f, portraitWidth, card.height - 24f);
            Tint(picture, new Color(.15f, .10f, .075f));
            var texture = GetPortrait(r);
            if (texture != null)
                GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit);
            else
                GUI.Label(picture, "No\nportrait", dateStyle);
            float tx = picture.xMax + 5f, tw = Mathf.Max(1f, card.xMax - tx - 10f);
            GUI.Label(new Rect(tx, card.y + 8f, tw, 39f), r.Name, nameStyle);
            Tint(new Rect(tx + 4f, card.y + 48f, Mathf.Max(1f, tw - 8f), 1f), new Color(.76f, .52f, .30f));
            GUI.Label(new Rect(tx, card.y + 50f, tw, 24f), MemorialDate(r), dateStyle);
            GUI.Label(new Rect(tx, card.y + 74f, tw, 23f), MemorialStatus(r), dateStyle);
        }

        private void DrawAlternatePlaque(Rect card, Casualty r, Texture2D plaqueTexture)
        {
            if (plaqueTexture != null)
                GUI.DrawTexture(card, plaqueTexture, ScaleMode.StretchToFill);
            else
                Tint(card, new Color(.23f, .24f, .26f));

            // Coordinates are normalized to the cleaned 397 x 187 plaque crop.
            // Keep the silver portrait frame, left-aligned lettering and steel
            // divider separate from the Classic bronze plaque presentation.
            Rect picture = AlternateRect(card, 50f, 35f, 106f, 125f);
            var texture = GetPortrait(r);
            if (texture != null)
                GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit);
            else
                GUI.Label(picture, "No portrait", alternateEmptyStyle);
            GUI.Label(AlternateRect(card, 181f, 21f, 178f, 68f), r.Name, alternateNameStyle);
            Tint(AlternateRect(card, 181f, 96f, 178f, 2f), new Color(.76f, .79f, .82f));
            GUI.Label(AlternateRect(card, 181f, 101f, 178f, 30f), MemorialDate(r), alternateDateStyle);
            GUI.Label(AlternateRect(card, 181f, 132f, 178f, 31f), MemorialStatus(r), alternateStatusStyle);
        }

        private static Rect AlternateRect(Rect card, float x, float y, float width, float height)
        {
            return new Rect(card.x + card.width * x / 397f, card.y + card.height * y / 187f,
                card.width * width / 397f, card.height * height / 187f);
        }

        private static string MemorialDate(Casualty r)
        {
            double ut = r.Fatal && r.DeathUT >= 0 ? r.DeathUT : r.UT;
            return ut < 0 || double.IsNaN(ut) || double.IsInfinity(ut) ? "Date unknown" : KSPUtil.PrintDateCompact(ut, false);
        }

        private static string MemorialStatus(Casualty r)
        {
            var tracker = CasualtyTracker.Instance;
            string status = r.Fatal ? (r.Closed ? "Respawned" : "Fallen") : "Missing";
            if (tracker != null && !tracker.IsLive(r))
                status += " • Archived";
            return status;
        }
        private Texture2D GetPortrait(Casualty r)
        {
            string data = r.PortraitPng ?? "";
            CachedPortrait p;
            if (!portraits.TryGetValue(r.Id, out p))
            {
                p = new CachedPortrait();
                portraits.Add(r.Id, p);
            }
            if (p.Data != data)
            {
                Release(ref p.Texture);
                p.Data = data;
                p.Texture = PortraitCache.Decode(data);
            }
            return p.Texture;
        }
        private static void Tint(Rect area, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }
        private void Resize()
        {
            var handle = new Rect(rect.width - 21, rect.height - 21, 20, 20);
            GUI.Label(handle, "//");
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
            {
                resizing = true;
                e.Use();
            }
            if (e.type == EventType.MouseDrag && resizing)
            {
                resizeDelta += e.delta;
                e.Use();
            }
            if (e.rawType == EventType.MouseUp)
                resizing = false;
        }
    }
}
