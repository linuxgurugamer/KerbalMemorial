using System;
using System.IO;
using System.Linq;
using ClickThroughFix;
using UnityEngine;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    // A single commemorative plaque, separate from the information window.
    // Artwork contains no sample name, date or portrait; all content comes from
    // the current casualty record. Theme follows the existing per-save setting.
    internal sealed class IndividualPlaqueWindow
    {
        private const float Aspect = 1576f / 766f;
        private Rect rect = new Rect(100, 100, 960, 540);
        private string selected, pending, portraitData;
        private bool open, artworkAttempted, alternate;
        private bool fading, closing, closeRequested;
        private bool interactionAllowed;
        private readonly bool deferClose;
        private float pendingFadeDuration, fadeDuration;
        private float fadeStartedAt, opacity = 1f;
        private Color plaqueTint;
        private Casualty record;
        private Texture2D classic, industrial, portrait;
        private GUIStyle label, inscription;
        private Vector2 noteScroll;
        private readonly PlaqueInfoWindow info = new PlaqueInfoWindow();

        internal IndividualPlaqueWindow(bool deferClose = false)
        {
            this.deferClose = deferClose;
        }

        internal bool IsOpen => open || pending != null;
        internal bool IsCloseRequested => closeRequested;
        internal Rect WindowRect => rect;

        internal void Open(string id, Rect parent, float fadeSeconds = 0f, bool alignToParent = false)
        {
            if (string.IsNullOrEmpty(id))
                return;
            pending = id;
            pendingFadeDuration = (float)FatalityPlaqueQueue.ClampFadeDuration(fadeSeconds);
            if (alignToParent)
                rect = parent;
            else if (!open)
            {
                rect.x = parent.center.x - rect.width / 2f;
                rect.y = parent.y + 32f;
            }
        }

        internal void Close()
        {
            info.Close();
            open = false;
            selected = pending = portraitData = null;
            record = null;
            Release(ref portrait);
            Release(ref classic);
            Release(ref industrial);
            artworkAttempted = false;
            noteScroll = Vector2.zero;
            pendingFadeDuration = fadeDuration = 0f;
            fading = false;
            closing = false;
            closeRequested = false;
            fadeStartedAt = -1f;
            opacity = 1f;
        }

        // Only the Close button requests an animated dismissal. Scene changes,
        // invalid records and parent-window teardown still close immediately.
        private void RequestClose()
        {
            if (fading || closeRequested)
                return;
            closeRequested = true;
            info.Close();
            // Automatic plaques let the queue coordinate both animations on Layout.
            if (!deferClose)
                BeginClose();
        }

        internal void BeginClose()
        {
            if (closing || !open)
                return;
            if (fadeDuration <= 0f)
            {
                Close();
                return;
            }
            info.Close();
            closing = true;
            fading = true;
            fadeStartedAt = -1f;
            opacity = 1f;
        }

        internal void OnGUI(int id, CasualtyTracker tracker, bool allowInput = true)
        {
            if (tracker == null)
            {
                Close();
                return;
            }
            interactionAllowed = allowInput;
            bool front = false;
            if (Event.current.type == EventType.Layout)
            {
                if (pending != null)
                {
                    if (selected != pending)
                    {
                        Release(ref portrait);
                        portraitData = null;
                        noteScroll = Vector2.zero;
                    }
                    // A wall selection always opens the plaque first, even for the same record.
                    info.Close();
                    selected = pending;
                    pending = null;
                    open = true;
                    front = true;
                    fadeDuration = pendingFadeDuration;
                    pendingFadeDuration = 0f;
                    closing = false;
                    closeRequested = false;
                    fading = fadeDuration > 0f;
                    fadeStartedAt = -1f;
                    opacity = fading ? 0f : 1f;
                }
                if (open)
                {
                    record = tracker.VisibleRecords().FirstOrDefault(r => r.Id == selected && Ledger.IsMemorialLoss(r));
                    if (record == null)
                    {
                        Close();
                        return;
                    }
                    alternate = CasualtyTracker.Options.alternateWallTheme;
                    EnsureArtwork();
                    string data = record.PortraitPng ?? "";
                    if (portraitData != data)
                    {
                        Release(ref portrait);
                        portraitData = data;
                        portrait = PortraitCache.Decode(data);
                    }
                }
            }
            if (!open || record == null)
                return;
            float width = Mathf.Min(960f, Mathf.Min(Screen.width, Mathf.Max(1f, Screen.height - 64f) * Aspect + 16f));
            rect.width = width;
            rect.height = Mathf.Min(Screen.height, (width - 16f) / Aspect + 64f);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            // Start on the first visible repaint, not at death confirmation or
            // during the configurable queue delay. Each new plaque starts over.
            // The reverse animation starts on the first repaint after Close.
            if (fading && Event.current.type == EventType.Repaint)
            {
                float now = Time.realtimeSinceStartup;
                if (fadeStartedAt < 0f)
                    fadeStartedAt = now;
                float t = Mathf.Clamp01((now - fadeStartedAt) / fadeDuration);
                opacity = closing ? Mathf.SmoothStep(1f, 0f, t) : Mathf.SmoothStep(0f, 1f, t);
                if (t >= 1f && !closing)
                {
                    fading = false;
                    opacity = 1f;
                }
            }
            Color original = GUI.color;
            plaqueTint = original;
            plaqueTint.a *= opacity;
            Rect moved;
            try
            {
                // Tint the window frame/title as well as its contents.
                GUI.color = plaqueTint;
                moved = ClickThruBlocker.GUIWindow(id, rect, Draw, "Individual Memorial Plaque");
            }
            finally
            {
                GUI.color = original;
            }
            rect.x = moved.x;
            rect.y = moved.y;
            // Finish after the final transparent repaint, outside the window
            // callback. IsOpen stays true until then, keeping the queue and
            // Flight Results gate active throughout the fade-out.
            if (closing && opacity <= 0f && Event.current.type == EventType.Repaint)
            {
                Close();
                return;
            }
            if (front && open)
                GUI.BringWindowToFront(id);
            if (open)
                info.OnGUI(id ^ 0x0000494e, tracker);
        }

        private void Draw(int id)
        {
            // GUI.Window can restore its own GUI state before invoking the
            // callback. Apply the same absolute tint, rather than multiplying twice.
            Color original = GUI.color;
            try
            {
                GUI.color = plaqueTint;
                DrawContents();
            }
            finally
            {
                GUI.color = original;
            }
        }

        private void DrawContents()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, richText = false, clipping = TextClipping.Clip, padding = new RectOffset(0,0,0,0) };
                inscription = new GUIStyle(label) { fontStyle = FontStyle.Normal, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }
            Rect area = new Rect(8f, 24f, rect.width - 16f, rect.height - 64f);
            Texture2D artwork = alternate ? industrial : classic;
            // The normalized overlay coordinates are specific to each template.
            Rect picture = alternate ? Section(area, .071f, .103f, .176f, .416f) : Section(area, .088f, .095f, .165f, .409f);
            Rect name = alternate ? Section(area, .29f, .105f, .64f, .19f) : Section(area, .357f, .115f, .515f, .17f);
            Rect date = alternate ? Section(area, .29f, .33f, .64f, .18f) : Section(area, .405f, .327f, .425f, .17f);
            Rect notes = alternate ? Section(area, .042f, .663f, .916f, .275f) : Section(area, .065f, .59f, .87f, .27f);
            Color ink = alternate ? new Color(.96f,.97f,.98f) : new Color(1f,.81f,.48f);
            if (Event.current.type == EventType.Repaint)
            {
                if (artwork != null)
                    GUI.DrawTexture(area, artwork, ScaleMode.StretchToFill);
                else
                    Fill(area, alternate ? new Color(.22f,.24f,.27f) : new Color(.25f,.16f,.09f));
                if (portrait != null)
                    GUI.DrawTexture(picture, portrait, ScaleMode.ScaleToFit);
                else
                    FitLabel(picture, "Portrait unavailable", ink, Mathf.Max(10, Mathf.RoundToInt(area.width * .018f)));
                FitLabel(name, record.Name ?? "Unknown", ink, Mathf.Max(12, Mathf.RoundToInt(area.width * .057f)));
                double ut = record.Fatal && record.DeathUT >= 0 ? record.DeathUT : record.UT;
                string when = ut < 0 || double.IsNaN(ut) || double.IsInfinity(ut) ? "Date unknown" : KSPUtil.PrintDateCompact(ut, false);
                FitLabel(date, when, ink, Mathf.Max(12, Mathf.RoundToInt(area.width * .042f)));
                if (alternate)
                    Fill(Section(area, .31f, .305f, .60f, .003f), new Color(.75f,.77f,.79f));
            }
            // Use the middle 80% of the inscription panel, with a large
            // font for short inscriptions and wrapping for longer messages.
            DrawInscription(notes, Inscriptions.Display(record), ink);
            // Keep the footer controls centered as a group and outside the artwork.
            float buttonWidth = Mathf.Min(110f, Mathf.Max(1f, (rect.width - 24f) / 2f));
            float left = (rect.width - (buttonWidth * 2f + 8f)) / 2f;
            float buttonY = rect.height - 34f;
            bool canInteract = interactionAllowed && !fading && !closeRequested;
            if (GUI.Button(new Rect(left, buttonY, buttonWidth, 26f), "Details") && canInteract)
                info.Open(record.Id, rect);
            bool close = GUI.Button(new Rect(left + buttonWidth + 8f, buttonY, buttonWidth, 26f), "Close");
            if (canInteract)
                GUI.DragWindow();
            if (close && canInteract)
                RequestClose();
        }

        private void DrawInscription(Rect panel, string text, Color ink)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var content = new GUIContent(text);
            float width = Mathf.Max(1f, panel.width * .8f);
            float height = Mathf.Max(1f, panel.height - 4f);
            // Allow a substantially larger font, but do not crowd the panel
            // vertically. Wrap at the chosen width before reducing font size.
            int maxSize = Mathf.Max(1, Mathf.FloorToInt(height * .65f));
            int minSize = Mathf.Min(maxSize, Mathf.Max(12, Mathf.RoundToInt(height * .22f)));
            inscription.fontSize = maxSize;
            inscription.normal.textColor = ink;
            inscription.alignment = TextAnchor.MiddleCenter;
            while (inscription.fontSize > minSize && inscription.CalcHeight(content, width) > height)
                inscription.fontSize--;
            float measured = inscription.CalcHeight(content, width);
            if (measured <= height)
            {
                noteScroll = Vector2.zero;
                GUI.Label(new Rect(panel.center.x - width / 2f, panel.y + 2f, width, height), content, inscription);
            }
            else
            {
                // Exceptionally long messages remain readable and scrollable.
                float scrollWidth = Mathf.Max(1f, panel.width - 22f);
                width = Mathf.Min(width, scrollWidth);
                float contentHeight = Mathf.Max(panel.height, inscription.CalcHeight(content, width) + 4f);
                noteScroll = GUI.BeginScrollView(panel, noteScroll, new Rect(0, 0, scrollWidth, contentHeight), false, true);
                GUI.Label(new Rect((scrollWidth - width) / 2f, 2f, width, contentHeight - 4f), content, inscription);
                GUI.EndScrollView();
            }
        }

        private static Rect Section(Rect a, float x, float y, float w, float h)
        {
            return new Rect(a.x + x*a.width, a.y + y*a.height, w*a.width, h*a.height);
        }
        private void FitLabel(Rect area, string text, Color color, int size)
        {
            var content = new GUIContent(text);
            label.fontSize = size;
            while (label.fontSize > 7 && (label.CalcSize(content).x > area.width || label.CalcSize(content).y > area.height))
                label.fontSize--;
            label.normal.textColor = new Color(0,0,0,.85f);
            GUI.Label(new Rect(area.x + 1, area.y + 2, area.width, area.height), content, label);
            label.normal.textColor = color;
            GUI.Label(area, content, label);
        }
        private static void Fill(Rect area, Color color)
        {
            Color old = GUI.color;
            color.a *= old.a; // Fallback artwork and divider must fade too.
            try
            {
                GUI.color = color;
                GUI.DrawTexture(area, Texture2D.whiteTexture);
            }
            finally
            {
                GUI.color = old;
            }
        }
        private void EnsureArtwork()
        {
            if (artworkAttempted)
                return;
            artworkAttempted = true;
            classic = Load("IndividualClassic");
            industrial = Load("IndividualAlternate");
        }
        private static Texture2D Load(string name)
        {
            Texture2D texture = null;
            try
            {
                using (var stream = typeof(IndividualPlaqueWindow).Assembly.GetManifestResourceStream("KerbalMemorial.Artwork." + name + ".png"))
                {
                    if (stream == null)
                        throw new InvalidOperationException("Missing artwork: " + name);
                    using (var data = new MemoryStream())
                    {
                        stream.CopyTo(data);
                        texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                        if (!texture.LoadImage(data.ToArray(), true))
                            throw new InvalidOperationException("Invalid artwork: " + name);
                    }
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }
            catch (Exception e)
            {
                Release(ref texture);
                Log.Warning("[KerbalMemorial] Individual plaque: " + e.Message);
                return null;
            }
        }
        private static void Release(ref Texture2D texture)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
