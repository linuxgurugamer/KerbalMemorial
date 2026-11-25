using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace KerbalMemorial
{
    // Optional, read-only integration. Do not reference KRASH.dll at build time.
    // KRASH sets KRASHShelter.persistent.shelterSimulationActive before the
    // simulated launch and clears it after restoring the pre-simulation game.
    // Unlike the pause-menu API, this flag covers launch and teardown as well.
    internal static class KrashIntegration
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static bool resolved, installed, warned;
        private static MemberInfo persistentMember, activeMember;
        private static Type persistentType;

        private static MemberInfo FindMember(Type type, string name, BindingFlags flags)
        {
            return (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
        }

        private static object Read(MemberInfo member, object target)
        {
            var field = member as FieldInfo;
            if (field != null) return field.GetValue(target);
            var property = member as PropertyInfo;
            return property == null ? null : property.GetValue(target, null);
        }

        private static void Resolve()
        {
            foreach (var loaded in AssemblyLoader.loadedAssemblies)
            {
                var assembly = loaded.assembly;
                if (assembly == null || !string.Equals(assembly.GetName().Name, "KRASH", StringComparison.OrdinalIgnoreCase)) continue;
                installed = true;
                Type shelter = assembly.GetType("KRASH.KRASHShelter", false);
                if (shelter != null) persistentMember = FindMember(shelter, "persistent", StaticFlags);
                break;
            }
            resolved = true;
        }

        internal static bool IsSimulationActive
        {
            get
            {
                try
                {
                    if (!resolved) Resolve();
                    if (!installed) return false;
                    object persistent = Read(persistentMember, null);
                    if (persistent != null && (!(persistent is UnityEngine.Object) || (UnityEngine.Object)persistent != null))
                    {
                        Type type = persistent.GetType();
                        if (persistentType != type)
                        {
                            persistentType = type;
                            activeMember = FindMember(type, "shelterSimulationActive", InstanceFlags);
                        }
                        object value = Read(activeMember, persistent);
                        if (value is bool) return (bool)value;
                    }
                }
                catch (Exception e)
                {
                    if (!warned) { Debug.LogWarning("[KerbalMemorial] KRASH state unavailable: " + e.Message); warned = true; }
                }
                if (!installed) return false;
                // During scenario initialization the persistent object may not
                // exist yet. KRASH creates this backup before setting its flag
                // and deletes it when it restores the game. Only use it if the
                // live flag is unavailable, never override a known false value.
                if (!warned && persistentMember == null)
                {
                    Debug.LogWarning("[KerbalMemorial] KRASH simulation flag not found; using its revert-file fallback.");
                    warned = true;
                }
                return !string.IsNullOrEmpty(HighLogic.SaveFolder) && File.Exists(Path.Combine(
                    KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder, "KRASHRevert.sfs"));
            }
        }
    }
}
