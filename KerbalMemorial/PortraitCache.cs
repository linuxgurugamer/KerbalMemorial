using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens.Flight;

namespace KerbalMemorial
{
    // Owns only PNG bytes. Never replaces or destroys KSP's live portrait textures.
    internal sealed class PortraitCache
    {
        private readonly Dictionary<string, string> pngs = new Dictionary<string, string>();
        private readonly Dictionary<string, float> retryAfter = new Dictionary<string, float>();
        private sealed class Pending
        {
            internal Texture Source;
            internal Kerbal Avatar;
            internal float ReadyReal, ReadyGame;
            internal int GoodFrames;
        }
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        private readonly HashSet<string> capturedThisFlight = new HashSet<string>();
        private const int MaxSize = 128;
        private const int MaxEncodedLength = 100000;

        internal void BeginFlight() { capturedThisFlight.Clear(); retryAfter.Clear(); pending.Clear(); }
        internal void Clear() { pngs.Clear(); BeginFlight(); }
        internal void Rename(string oldName, string newName)
        {
            if (pngs.TryGetValue(oldName, out string png)) { pngs.Remove(oldName); pngs[newName] = png; }
            if (capturedThisFlight.Remove(oldName)) capturedThisFlight.Add(newName);
            retryAfter.Remove(oldName); retryAfter.Remove(newName);
            pending.Remove(oldName); pending.Remove(newName);
        }
        internal string Get(string name) => pngs.TryGetValue(name, out string png) ? png : "";
        internal void Capture(ProtoCrewMember crew)
        {
            if (crew == null || string.IsNullOrEmpty(crew.name) || !HighLogic.LoadedSceneIsFlight ||
                crew.rosterStatus != ProtoCrewMember.RosterStatus.Assigned || capturedThisFlight.Contains(crew.name)) return;
            if (retryAfter.TryGetValue(crew.name, out float after) && Time.realtimeSinceStartup < after) return;
            retryAfter[crew.name] = Time.realtimeSinceStartup + 0.2f;
            try
            {
                // Prefer the Kerbal's own render target, not the gallery's UI image
                // (which can display the stock startup/death static overlay).
                var kerbal = crew.KerbalRef;
                if (kerbal != null && kerbal.protoCrewMember != crew) kerbal = null;
                RenderTexture gallerySource = null;
                var gallery = KerbalPortraitGallery.Instance;
                if (gallery != null && gallery.Portraits != null)
                    foreach (var portrait in gallery.Portraits)
                    {
                        if (portrait == null || portrait.crewPcm != crew) continue;
                        if (kerbal == null && portrait.crewMember != null && portrait.crewMember.protoCrewMember == crew)
                            kerbal = portrait.crewMember;
                        // A fallback for EVA or other crew with no IVA avatar. Never use a
                        // static Texture2D or the overlay image as a portrait source.
                        if (portrait.portrait != null)
                            gallerySource = portrait.portrait.texture as RenderTexture;
                        break;
                    }
                if (kerbal != null && kerbal.state != Kerbal.States.ALIVE)
                { pending.Remove(crew.name); return; }
                var source = kerbal != null && kerbal.avatarTexture != null ? kerbal.avatarTexture : gallerySource;
                if (source == null || !source.IsCreated()) { pending.Remove(crew.name); return; }

                // Start the wait when THIS avatar becomes available, not at scene load.
                // Stock's default staticOverlayDuration is 3 seconds. Include a margin,
                // even for modified durations, and do not let time warp skip the wait.
                if (!pending.TryGetValue(crew.name, out Pending wait) || wait.Source != source || wait.Avatar != kerbal)
                {
                    float delay = Mathf.Max(5f, (kerbal != null ? kerbal.staticOverlayDuration : 3f) + 1f);
                    pending[crew.name] = new Pending { Source = source, Avatar = kerbal,
                        ReadyReal = Time.realtimeSinceStartup + delay, ReadyGame = Time.time + delay };
                    return;
                }
                if (Time.realtimeSinceStartup < wait.ReadyReal || Time.time < wait.ReadyGame) return;
                retryAfter[crew.name] = Time.realtimeSinceStartup + 0.5f;
                string png = Snapshot(source);
                // Hidden crew may have an unrendered target. Only render their own
                // existing camera after the readiness gate, without changing its target.
                if (string.IsNullOrEmpty(png) && kerbal != null && kerbal.kerbalCam != null &&
                    kerbal.kerbalCam.gameObject.activeInHierarchy && kerbal.kerbalCam.targetTexture == source)
                {
                    RenderTexture previous = RenderTexture.active;
                    try { kerbal.kerbalCam.Render(); png = Snapshot(source); }
                    finally { RenderTexture.active = previous; }
                }
                if (string.IsNullOrEmpty(png)) { wait.GoodFrames = 0; return; }
                // Require two acceptable samples on different polls. Never commit the
                // first nonblank frame, and keep any previous good portrait meanwhile.
                if (++wait.GoodFrames < 2) return;
                pngs[crew.name] = png;
                capturedThisFlight.Add(crew.name);
                retryAfter.Remove(crew.name);
                pending.Remove(crew.name);
            }
            catch (Exception e) { pending.Remove(crew.name); Debug.LogWarning("[KerbalMemorial] Portrait capture: " + e.Message); retryAfter[crew.name] = Time.realtimeSinceStartup + 2f; }
        }
        private static string Snapshot(Texture source)
        {
            if (source.width < 2 || source.height < 2) return "";
            if (source is RenderTexture rt && !rt.IsCreated()) return "";
            float scale = Mathf.Min(1f, (float)MaxSize / Mathf.Max(source.width, source.height));
            int w = Mathf.Max(2, Mathf.RoundToInt(source.width * scale)), h = Mathf.Max(2, Mathf.RoundToInt(source.height * scale));
            RenderTexture previous = RenderTexture.active;
            RenderTexture temp = null;
            Texture2D image = null;
            try
            {
                temp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temp);
                RenderTexture.active = temp;
                image = new Texture2D(w, h, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                image.Apply(false, false);
                if (!UsableImage(image)) return "";
                return Convert.ToBase64String(image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (temp != null) RenderTexture.ReleaseTemporary(temp);
                if (image != null) UnityEngine.Object.Destroy(image);
            }
        }
        internal void Read(ConfigNode node)
        {
            Clear();
            foreach (var child in node.GetNodes("CREW_PORTRAIT"))
            {
                try
                {
                    string name = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(child.GetValue("name") ?? ""));
                    string png = child.GetValue("png") ?? "";
                    if (name.Length > 0)
                    {
                        var decoded = Decode(png);
                        if (decoded != null) { pngs[name] = png; UnityEngine.Object.Destroy(decoded); }
                    }
                }
                catch (FormatException) { }
            }
        }
        internal void Write(ConfigNode node)
        {
            node.RemoveNodes("CREW_PORTRAIT");
            foreach (var pair in pngs)
            {
                var child = node.AddNode("CREW_PORTRAIT");
                child.AddValue("name", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pair.Key)));
                child.AddValue("png", pair.Value);
            }
        }
        private static bool UsableImage(Texture2D image)
        {
            var pixels = image.GetPixels32();
            var luminance = new int[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                luminance[i] = pixels[i].r + pixels[i].g + pixels[i].b;
            return PortraitQuality.IsUsable(luminance, image.width, image.height);
        }
        private static bool IsValid(string png) => !string.IsNullOrEmpty(png) && png.Length <= MaxEncodedLength;
        internal static Texture2D Decode(string png)
        {
            if (!IsValid(png)) return null;
            Texture2D image = null;
            try
            {
                byte[] data = Convert.FromBase64String(png);
                // Validate PNG signature and dimensions before asking Unity to decode it.
                if (data.Length < 24 || data[0] != 137 || data[1] != 80 || data[2] != 78 || data[3] != 71 ||
                    data[4] != 13 || data[5] != 10 || data[6] != 26 || data[7] != 10 ||
                    data[12] != 73 || data[13] != 72 || data[14] != 68 || data[15] != 82) return null;
                uint w = ((uint)data[16] << 24) | ((uint)data[17] << 16) | ((uint)data[18] << 8) | data[19];
                uint h = ((uint)data[20] << 24) | ((uint)data[21] << 16) | ((uint)data[22] << 8) | data[23];
                if (w == 0 || h == 0 || w > MaxSize || h > MaxSize) return null;
                image = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (image.LoadImage(data, false) && UsableImage(image)) { image.Apply(false, true); return image; }
            }
            catch (Exception e) { Debug.LogWarning("[KerbalMemorial] Portrait decode: " + e.Message); }
            if (image != null) UnityEngine.Object.Destroy(image);
            return null;
        }
    }
}
