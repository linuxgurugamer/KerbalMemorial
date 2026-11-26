using System;
using System.Globalization;
using System.IO;
using UnityEngine;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    // Global installation-level overrides, kept outside the GameDatabase so
    // a normal game save, revert or mod update does not replace them.
    internal static class SignSettings
    {
        private const string NodeName = "KERBAL_MEMORIAL_SIGN_SETTINGS";
        private static ConfigNode overrides;
        private static bool loaded;
        internal static int Revision
        {
            get;
            private set;
        }
        private static string SettingsPath => Path.Combine(KSPUtil.ApplicationRootPath,
            "GameData", "KerbalMemorial", "PluginData", "SignSettings.cfg");

        private static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            overrides = new ConfigNode(NodeName);
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var root = ConfigNode.Load(SettingsPath);
                    var node = root == null ? null : root.GetNode(NodeName);
                    if (node == null)
                        throw new IOException("Invalid sign settings file.");
                    overrides = node;
                }
            }
            catch (Exception e)
            {
                Log.Warning("[KerbalMemorial] Could not load sign settings: " + e.Message);
            }
        }

        internal static bool TryValue(string text, float min, float max, out float value)
        {
            return (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            && !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        }

        internal static string Format(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

        internal static void ApplyOverrides(EditorFacility facility, ref float width, ref float height, ref bool postMounted)
        {
            EnsureLoaded();
            var node = overrides.GetNode(facility.ToString());
            if (node == null)
                return;
            if (TryValue(node.GetValue("widthMeters"), 0f, 100f, out float w))
                width = w;
            if (TryValue(node.GetValue("heightAboveFloor"), -1f, 100f, out float h) && (h == -1f || h >= 0f))
                height = h;
            if (bool.TryParse(node.GetValue("postMounted"), out bool posts))
                postMounted = posts;
        }

        // Commit only the field actually changed. Opening the sliders or
        // changing one building does not replace untouched legacy settings.
        internal static void SaveValue(EditorFacility facility, bool isWidth, float value)
        {
            if (facility != EditorFacility.VAB && facility != EditorFacility.SPH)
                throw new ArgumentOutOfRangeException(nameof(facility));
            float min = isWidth ? 1f : .1f;
            float max = isWidth ? 5f : 10f;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value));

            SaveField(facility, isWidth ? "widthMeters" : "heightAboveFloor", Format(value));
        }

        internal static void SaveMount(EditorFacility facility, bool postMounted)
        {
            SaveField(facility, "postMounted", postMounted ? "true" : "false");
        }

        private static void SaveField(EditorFacility facility, string field, string value)
        {
            if (facility != EditorFacility.VAB && facility != EditorFacility.SPH)
                throw new ArgumentOutOfRangeException(nameof(facility));
            EnsureLoaded();
            var root = new ConfigNode();
            var node = root.AddNode(NodeName);
            foreach (var building in new[] { EditorFacility.VAB, EditorFacility.SPH })
            {
                var previous = overrides.GetNode(building.ToString());
                if (previous == null && building != facility)
                    continue;
                var updated = node.AddNode(building.ToString());
                foreach (string key in new[] { "widthMeters", "heightAboveFloor", "postMounted" })
                {
                    string text = previous == null ? null : previous.GetValue(key);
                    if (building == facility && key == field)
                        text = value;
                    if (text != null)
                        updated.AddValue(key, text);
                }
            }
            string path = SettingsPath, temp = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            root.Save(temp);
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else
                File.Move(temp, path);
            // Publish only after a successful write. LateUpdate sees the new
            // revision and moves/resizes the existing sign on the next frame.
            overrides = node;
            loaded = true;
            ++Revision;
        }
    }
}
