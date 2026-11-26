using System;
using System.Reflection;
using UnityEngine;

namespace KerbalMemorialIntegration
{
    /// <summary>
    /// Optional Kerbal Memorial integration for KSP1 mods. Copy this source
    /// file into your own project; do not reference KerbalMemorial.dll.
    /// Call on the main Unity thread when an actual fatality is committed.
    /// </summary>
    internal static class KerbalMemorialWrapper
    {
        private delegate bool DeathReporter(ProtoCrewMember crew, string cause, string sourceMod);
        private static DeathReporter reporter;
        private static bool warned;

        /// <summary>True if the compatible API is loaded, not necessarily ready for reports.</summary>
        public static bool IsAvailable
        {
            get
            {
                return Resolve() != null;
            }
        }

        /// <summary>
        /// Report an actual death, without changing roster status. Returns false
        /// if the mod is absent, incompatible, unavailable, in a KRASH simulation,
        /// or rejects the report. The caller must handle its own gameplay effects.
        /// </summary>
        public static bool ReportDeath(ProtoCrewMember crew, string cause, string sourceMod)
        {
            if (crew == null || string.IsNullOrWhiteSpace(cause))
                return false;
            try
            {
                DeathReporter method = Resolve();
                return method != null && method(crew, cause, sourceMod);
            }
            catch (Exception ex)
            {
                WarnOnce(ex);
                return false;
            }
        }

        private static DeathReporter Resolve()
        {
            if (reporter != null)
                return reporter;
            try
            {
                // Search loaded assemblies only; never load or require the optional mod.
                // Cache a successful binding, not a failed lookup (e.g. early startup).
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.Equals(assembly.GetName().Name, "KerbalMemorial", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Type api = assembly.GetType("KerbalMemorial.MemorialAPI", false);
                    if (api == null)
                        continue;
                    MethodInfo method = api.GetMethod("ReportDeath", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(ProtoCrewMember), typeof(string), typeof(string) }, null);
                    if (method == null || method.ReturnType != typeof(bool))
                        continue;
                    reporter = (DeathReporter)Delegate.CreateDelegate(typeof(DeathReporter), method, false);
                    if (reporter != null)
                        return reporter;
                }
            }
            catch (Exception ex)
            {
                WarnOnce(ex);
            }
            return null;
        }

        private static void WarnOnce(Exception ex)
        {
            if (warned)
                return;
            warned = true;
            Debug.LogWarning("[KerbalMemorialWrapper] Optional death-report API failed: " + ex);
        }
    }
}
