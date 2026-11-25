using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace KerbalMemorial
{
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

        internal static string Pick(string directory, Action<string> warning = null)
        { return Pick(ReadPool(directory, warning)); }

        internal static string Pick(IList<string> messages)
        { return messages == null || messages.Count == 0 ? "" : messages[random.Next(messages.Count)]; }


        // Any confirmed fatality without an inscription is eligible, including
        // historical imports and respawned crew. Never replace an existing message.
        internal static bool Assign(Casualty record, Func<string> pick)
        {
            if (record == null || !record.Fatal || !string.IsNullOrWhiteSpace(record.Inscription) || pick == null) return false;
            string message = pick();
            if (string.IsNullOrWhiteSpace(message)) return false;
            record.Inscription = message;
            return true;
        }

        // Reuse the inscription from the other timeline copy before choosing a new
        // message. Return true only when the archive copy changes.
        internal static bool FillPair(Casualty live, Casualty archived, Func<string> pick)
        {
            Assign(live, () => !string.IsNullOrWhiteSpace(archived?.Inscription) ? archived.Inscription : pick?.Invoke());
            return Assign(archived, () => !string.IsNullOrWhiteSpace(live?.Inscription) ? live.Inscription : pick?.Invoke());
        }

        internal static string Display(Casualty record)
        {
            if (record == null) return "";
            // Preserve the original notes-based display for older records.
            return string.IsNullOrWhiteSpace(record.Inscription) ? (record.Notes ?? "") : record.Inscription;
        }
    }
}
