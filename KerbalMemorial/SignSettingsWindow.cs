using System;
using UnityEngine;
using ClickThroughFix;

namespace KerbalMemorial
{
    internal sealed class SignSettingsWindow
    {
        private Rect rect = new Rect(160, 130, 410, 335);
        private float vabWidth, vabHeight, sphWidth, sphHeight;
        private bool vabPosts, sphPosts;
        private const float MinWidth = 1f, MaxWidth = 5f;
        private const float MinHeight = .1f, MaxHeight = 10f;
        private string message = "";
        private GUIStyle wrap;
        internal bool IsOpen
        {
            get;
            private set;
        }

        internal void Open(Rect parent)
        {
            if (IsOpen)
                return;
            var vab = EditorAccidentSign.SignLayout.Load(EditorFacility.VAB);
            var sph = EditorAccidentSign.SignLayout.Load(EditorFacility.SPH);
            vabWidth = Snap(vab.WidthMeters, MinWidth, MaxWidth);
            vabHeight = Snap(vab.HeightAboveFloor, MinHeight, MaxHeight);
            sphWidth = Snap(sph.WidthMeters, MinWidth, MaxWidth);
            sphHeight = Snap(sph.HeightAboveFloor, MinHeight, MaxHeight);
            vabPosts = vab.PostMounted;
            sphPosts = sph.PostMounted;
            rect.x = parent.x + (parent.width - rect.width) / 2f;
            rect.y = parent.y + 35f;
            message = "";
            IsOpen = true;
        }

        internal void Close()
        {
            IsOpen = false;
        }

        internal void OnGUI(int id)
        {
            if (!IsOpen)
                return;
            if (wrap == null)
                wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            rect.width = Mathf.Min(410f, Screen.width);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            rect = ClickThruBlocker.GUILayoutWindow(id, rect, Draw, "Accident Sign Settings", GUILayout.Width(rect.width));
        }

        private void Draw(int id)
        {
            GUILayout.Label("Vehicle Assembly Building (VAB)");
            Slider("Width", ref vabWidth, MinWidth, MaxWidth, EditorFacility.VAB, true);
            Slider("Height above floor", ref vabHeight, MinHeight, MaxHeight, EditorFacility.VAB, false);
            Mount(ref vabPosts, EditorFacility.VAB);
            GUILayout.Space(8);
            GUILayout.Label("Spaceplane Hangar (SPH)");
            Slider("Width", ref sphWidth, MinWidth, MaxWidth, EditorFacility.SPH, true);
            Slider("Height above floor", ref sphHeight, MinHeight, MaxHeight, EditorFacility.SPH, false);
            Mount(ref sphPosts, EditorFacility.SPH);
            GUILayout.Space(8);
            GUILayout.Label("Width: 1.0-5.0 m. Height: 0.1-10.0 m to the bottom edge of the sign.", wrap);
            GUILayout.Label(string.IsNullOrEmpty(message) ? "Changes apply and save immediately." : message, wrap);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(100)))
                    Close();
                GUILayout.FlexibleSpace();
            }
            GUI.DragWindow();
        }

        private void Mount(ref bool postMounted, EditorFacility facility)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Mounting", GUILayout.Width(145));
                int next = GUILayout.SelectionGrid(postMounted ? 1 : 0, new[] { "Wall", "Posts" }, 2);
                bool usePosts = next == 1;
                if (usePosts == postMounted)
                    return;
                try
                {
                    SignSettings.SaveMount(facility, usePosts);
                    postMounted = usePosts;
                    message = "";
                }
                catch (Exception e)
                {
                    message = "Save failed: " + e.Message;
                }
            }
        }

        private void Slider(string label, ref float value, float min, float max,
            EditorFacility facility, bool isWidth)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(145));
                float next = Snap(GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true)), min, max);
                if (next != value)
                {
                    try
                    {
                        SignSettings.SaveValue(facility, isWidth, next);
                        value = next;
                        message = "";
                    }
                    catch (Exception e)
                    {
                        message = "Save failed: " + e.Message;
                    }
                }
                GUILayout.Label(SignSettings.Format(value) + " m", GUILayout.Width(64));
            }
        }

        private static float Snap(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return min;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(value, min, max) * 10f) / 10f, min, max);
        }
    }
}
