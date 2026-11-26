using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace KerbalMemorial
{
    // One shuffle cycle per status. Keep used messages even if a file is
    // temporarily removed, or the associated missing crew member returns.
    internal sealed class InscriptionCycle
    {
        internal readonly HashSet<string> Used = new HashSet<string>(StringComparer.Ordinal);
        internal bool Initialized { get; private set; }
        internal void Restore(IEnumerable<string> used)
        {
            Used.Clear();
            foreach (string message in used)
                if (!string.IsNullOrWhiteSpace(message)) Used.Add(message);
            Initialized = true;
        }
        internal void Seed(IEnumerable<string> existing)
        {
            if (!Initialized) Restore(existing);
        }
        internal string Pick(IEnumerable<string> messages)
        {
            var pool = messages.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.Ordinal).ToList();
            if (pool.Count == 0) return ""; // Do not reset on an empty folder.
            var unused = pool.Where(m => !Used.Contains(m)).ToList();
            if (unused.Count == 0) { Used.Clear(); unused = pool; }
            string chosen = Inscriptions.Pick(unused);
            Used.Add(chosen);
            Initialized = true;
            return chosen;
        }
    }

    internal sealed class InscriptionCycles
    {
        internal readonly InscriptionCycle Deceased = new InscriptionCycle();
        internal readonly InscriptionCycle Missing = new InscriptionCycle();
        internal InscriptionCycle For(bool missing) => missing ? Missing : Deceased;
        internal void Seed(IEnumerable<Casualty> records)
        {
            Deceased.Seed(records.Select(Inscriptions.FatalMessage));
            Missing.Seed(records.Select(Inscriptions.SavedMissingMessage));
        }
        internal void CopyFrom(InscriptionCycles other)
        {
            if (other.Deceased.Initialized) Deceased.Restore(other.Deceased.Used);
            if (other.Missing.Initialized) Missing.Restore(other.Missing.Used);
        }
    }

    // No Unity dependencies: this parser and the assignment guard can be tested offline.
    internal static class Inscriptions
    {
        private static readonly Random random = new Random();
        private static readonly Regex heading = new Regex(@"^#{1,6}(?:\s|$)");
        private static readonly Regex listMarker = new Regex(@"^(?:[-*+•]\s+|\d+[.)]\s+)");
        private static readonly Regex divider = new Regex(@"^(?:-\s*){3,}$|^(?:\*\s*){3,}$|^(?:_\s*){3,}$");

        internal static IEnumerable<string> Parse(IEnumerable<string> lines)
        {
            if (lines == null) yield break;
            foreach (string line in lines)
            {
                string text = (line ?? "").Trim().TrimStart('\uFEFF').Trim();
                if (text.Length == 0 || heading.IsMatch(text) || divider.IsMatch(text)) continue;
                text = listMarker.Replace(text, "", 1).Trim();
                // Remove enclosing Markdown emphasis, not punctuation within the message.
                foreach (string marker in new[] { "***", "___", "**", "__", "*", "_" })
                    if (text.Length > marker.Length * 2 && text.StartsWith(marker, StringComparison.Ordinal) && text.EndsWith(marker, StringComparison.Ordinal))
                    { text = text.Substring(marker.Length, text.Length - 2 * marker.Length).Trim(); break; }
                if (text.Length != 0 && !heading.IsMatch(text) && !divider.IsMatch(text)) yield return text;
            }
        }

        internal static List<string> ReadPool(string directory, Action<string> warning = null)
        {
            var messages = new List<string>();
            string[] files;
            try
            {
                if (!Directory.Exists(directory)) return messages;
                files = Directory.GetFiles(directory);
            }
            catch (Exception e)
            { warning?.Invoke("Cannot enumerate inscriptions: " + e.Message); return messages; }
            foreach (string file in files)
            {
                if (!string.Equals(Path.GetExtension(file), ".txt", StringComparison.OrdinalIgnoreCase)) continue;
                try { messages.AddRange(Parse(File.ReadAllLines(file))); }
                catch (Exception e) { warning?.Invoke("Cannot read inscription file " + Path.GetFileName(file) + ": " + e.Message); }
            }
            return messages;
        }

        // Do not mix the two pools, or read old files in the parent directory.
        internal static List<string> ReadStatusPool(string root, bool missing, Action<string> warning = null)
        { return ReadPool(Path.Combine(root, missing ? "Missing" : "Deceased"), warning); }

        internal static string Pick(string directory, Action<string> warning = null)
        { return Pick(ReadPool(directory, warning)); }

        internal static string Pick(IList<string> messages)
        { return messages == null || messages.Count == 0 ? "" : messages[random.Next(messages.Count)]; }


        internal const string MissingMessage = "Missing, but not forgotten";

        private static bool IsMissing(Casualty record)
        { return record != null && !record.Fatal && !record.Closed && record.Status == "Missing"; }

        // Only reuse a message saved for the same kind of casualty. The new
        // flag distinguishes a real random missing inscription from the old
        // fixed fallback, even if the file contains exactly the same words.
        internal static string FatalMessage(Casualty record)
        { return record != null && record.Fatal && !record.InscriptionForMissing && !record.InscriptionFromMissingPool ? record.Inscription : null; }

        internal static string SavedMissingMessage(Casualty record)
        { return IsMissing(record) && record.InscriptionForMissing && record.InscriptionFromMissingPool ? record.Inscription : null; }

        internal static bool NeedsMessage(Casualty record)
        {
            if (record == null) return false;
            if (IsMissing(record)) return string.IsNullOrWhiteSpace(SavedMissingMessage(record));
            return record.InscriptionForMissing || record.InscriptionFromMissingPool || (record.Fatal && string.IsNullOrWhiteSpace(record.Inscription));
        }

        internal static bool Assign(Casualty record, Func<string> pick, Func<string> pickMissing = null)
        {
            if (!NeedsMessage(record)) return false;
            if (IsMissing(record))
            {
                string message = pickMissing?.Invoke();
                bool fromPool = !string.IsNullOrWhiteSpace(message);
                if (!fromPool) message = MissingMessage;
                bool updated = record.Inscription != message || !record.InscriptionForMissing || record.InscriptionFromMissingPool != fromPool;
                record.Inscription = message;
                record.InscriptionForMissing = true;
                record.InscriptionFromMissingPool = fromPool;
                return updated;
            }
            // Returned crew lose the temporary message; confirmed fatalities
            // must receive a deceased inscription, never a missing inscription.
            bool changed = record.InscriptionForMissing || record.InscriptionFromMissingPool;
            if (changed)
            {
                record.Inscription = "";
                record.InscriptionForMissing = false;
                record.InscriptionFromMissingPool = false;
            }
            if (!record.Fatal || pick == null) return changed;
            string tribute = pick();
            if (string.IsNullOrWhiteSpace(tribute)) return changed;
            record.Inscription = tribute;
            return true;
        }

        // Reuse an existing inscription from the matching timeline copy before
        // drawing from the appropriate pool. Do not copy a fallback as a random
        // selection, or copy an inscription across casualty status categories.
        internal static bool AssignFrom(Casualty record, Casualty other, Func<string> pick, Func<string> pickMissing = null)
        {
            return Assign(record,
                () => !string.IsNullOrWhiteSpace(FatalMessage(other)) ? FatalMessage(other) : pick?.Invoke(),
                () => !string.IsNullOrWhiteSpace(SavedMissingMessage(other)) ? SavedMissingMessage(other) : pickMissing?.Invoke());
        }

        internal static bool FillPair(Casualty live, Casualty archived, Func<string> pick, Func<string> pickMissing = null)
        {
            AssignFrom(live, archived, pick, pickMissing);
            return AssignFrom(archived, live, pick, pickMissing);
        }

        // Operate on actual ledger records, not UI copies. An archive, when
        // enabled, shares existing inscriptions with matching live records.
        // Return true if an archive record changed and needs to be persisted.
        internal static bool Backfill(IEnumerable<Casualty> live, IDictionary<string, Casualty> archive, Func<string> pick, Func<string> pickMissing = null)
        {
            bool archiveChanged = false;
            var liveIds = archive == null ? null : new HashSet<string>();
            foreach (var record in live)
            {
                Casualty archived = null;
                if (archive != null)
                {
                    liveIds.Add(record.Id);
                    archive.TryGetValue(record.Id, out archived);
                }
                if (FillPair(record, archived, pick, pickMissing)) archiveChanged = true;
            }
            if (archive != null)
                foreach (var record in archive.Values)
                    if (!liveIds.Contains(record.Id) && Assign(record, pick, pickMissing)) archiveChanged = true;
            return archiveChanged;
        }

        internal static string Display(Casualty record)
        {
            if (record == null) return "";
            if (IsMissing(record)) return string.IsNullOrWhiteSpace(SavedMissingMessage(record)) ? MissingMessage : record.Inscription;
            // Preserve the original notes-based display if a fatality message is unavailable.
            return record.InscriptionForMissing || record.InscriptionFromMissingPool || string.IsNullOrWhiteSpace(record.Inscription) ? (record.Notes ?? "") : record.Inscription;
        }
    }
}
