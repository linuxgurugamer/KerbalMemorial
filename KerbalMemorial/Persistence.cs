using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace KerbalMemorial
{
    internal static class Persistence
    {
        // Only the explicitly declared data fields of these two DTOs are serialized.
        private static void WriteFields(object obj, ConfigNode node)
        {
            foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = f.GetValue(obj);
                if (f.FieldType == typeof(Context)) { if (value != null) { var child = node.AddNode("LOCATION"); WriteFields(value, child); } }
                else if (f.FieldType == typeof(double)) node.AddValue(f.Name, ((double)value).ToString("R", CultureInfo.InvariantCulture));
                else if (f.FieldType == typeof(string)) node.AddValue(f.Name, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes((string)value ?? "")));
                else if (f.FieldType == typeof(bool)) node.AddValue(f.Name, value.ToString());
            }
        }
        private static void ReadFields(object obj, ConfigNode node)
        {
            foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string s = node.GetValue(f.Name);
                if (f.FieldType == typeof(Context)) { var child = node.GetNode("LOCATION"); if (child != null) { var context = new Context(); ReadFields(context, child); f.SetValue(obj, context); } }
                else if (s != null && f.FieldType == typeof(string)) { try { f.SetValue(obj, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s))); } catch (FormatException) { } }
                else if (f.FieldType == typeof(double) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) f.SetValue(obj, d);
                else if (f.FieldType == typeof(bool) && bool.TryParse(s, out bool b)) f.SetValue(obj, b);
            }
        }
        internal static void Write(ConfigNode node, IEnumerable<Casualty> records)
        {
            node.RemoveNodes("CASUALTY");
            foreach (var r in records) WriteFields(r, node.AddNode("CASUALTY"));
        }
        internal static List<Casualty> Read(ConfigNode node)
        {
            var list = new List<Casualty>();
            if (node == null) return list;
            var seen = new HashSet<string>();
            foreach (var child in node.GetNodes("CASUALTY"))
            {
                var r = new Casualty { Id = "" }; ReadFields(r, child);
                if (r.Id.Length > 0 && r.Name.Length > 0 && seen.Add(r.Id)) list.Add(r);
            }
            return list;
        }
        internal static void WriteCycles(ConfigNode node, InscriptionCycles cycles)
        {
            node.RemoveNodes("INSCRIPTION_CYCLE");
            foreach (bool missing in new[] { false, true })
            {
                var cycle = cycles.For(missing);
                if (!cycle.Initialized) continue;
                var child = node.AddNode("INSCRIPTION_CYCLE");
                child.AddValue("status", missing ? "Missing" : "Deceased");
                foreach (string message in cycle.Used.OrderBy(m => m, StringComparer.Ordinal))
                    child.AddValue("used", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));
            }
        }
        internal static InscriptionCycles ReadCycles(ConfigNode node)
        {
            var cycles = new InscriptionCycles();
            foreach (var child in node.GetNodes("INSCRIPTION_CYCLE"))
            {
                string status = child.GetValue("status");
                if (status != "Missing" && status != "Deceased") continue;
                var used = new List<string>();
                foreach (string encoded in child.GetValues("used"))
                    try { used.Add(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded))); }
                    catch (FormatException) { }
                cycles.For(status == "Missing").Restore(used);
            }
            return cycles;
        }
        internal static string DirectoryPath { get { return Path.Combine(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder, "KerbalMemorial"); } }
        internal static string ArchivePath { get { return Path.Combine(DirectoryPath, "Casualties.cfg"); } }
        internal static List<Casualty> LoadArchive(out InscriptionCycles cycles)
        {
            if (!File.Exists(ArchivePath) && !File.Exists(ArchivePath + ".bak")) { cycles = new InscriptionCycles(); return new List<Casualty>(); }
            try { return ReadArchive(ArchivePath, out cycles); }
            catch { return ReadArchive(ArchivePath + ".bak", out cycles); }
        }
        private static List<Casualty> ReadArchive(string path, out InscriptionCycles cycles)
        {
            var root = ConfigNode.Load(path);
            var node = root == null ? null : (root.GetNode("KERBAL_MEMORIAL") ?? root);
            if (node == null || node.GetValue("format") != "1") throw new IOException("Invalid archive: " + path);
            cycles = ReadCycles(node);
            return Read(node);
        }
        internal static void SaveArchive(IEnumerable<Casualty> records, InscriptionCycles cycles)
        {
            Directory.CreateDirectory(DirectoryPath);
            var root = new ConfigNode(); var node = root.AddNode("KERBAL_MEMORIAL"); node.AddValue("format", "1"); Write(node, records); WriteCycles(node, cycles);
            string path = ArchivePath, temp = path + ".tmp";
            root.Save(temp);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        internal static string Export(IEnumerable<Casualty> records, HashSet<string> live)
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "Casualties-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            var lines = new List<string> { "Name,OriginalName,Profession,Gender,Type,Level,Status,Fatal,UT,DeathUT,ReturnUT,Timeline,Vessel,VesselID,Part,Body,Situation,EVA,Latitude,Longitude,Altitude_m,Speed_m_s,G,MET_s,ContextUT,ContextQuality,Cause,Confidence,Source,Notes,History,ID,Inscription" };
            foreach (var r in records)
            {
                var c = r.Location ?? new Context();
                lines.Add(string.Join(",", new[] { r.Name,r.OriginalName,r.Trait,r.Gender,r.CrewType,r.Level,r.DisplayStatus,r.Fatal.ToString(), TimeValue(r.UT),TimeValue(r.DeathUT),TimeValue(r.ReturnUT),live.Contains(r.Id)?"Current":"Archived",c.Vessel,c.VesselId,c.PartTitle,c.Body,c.Situation,c.EVA,Ledger.Number(c.Latitude),Ledger.Number(c.Longitude),Ledger.Number(c.Altitude),Ledger.Number(c.Speed),Ledger.Number(c.Gee),Ledger.Number(c.MET),TimeValue(c.UT),c.Quality,r.Cause,r.Confidence,r.Source,r.Notes,r.History,r.Id,r.Inscription }.Select(Ledger.Csv).ToArray()));
            }
            File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(true)); return path;
        }
        private static string TimeValue(double n) { return n < 0 ? "" : Ledger.Number(n); }
    }
}
