using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KerbalMemorial
{
    public sealed class Casualty
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "", OriginalName = "", Trait = "", Gender = "", CrewType = "", Level = "";
        public string Status = "Missing", Cause = "Unknown", Confidence = "Unknown", Source = "", Notes = "", History = "";
        public string PortraitPng = "", Inscription = "";
        public double UT = -1, DeathUT = -1, ReturnUT = -1;
        public bool ReportedKilled, Imported, ExternalCause;
        public Context Location;
        public bool Closed { get { return ReturnUT >= 0; } }
        public bool Fatal { get { return ReportedKilled || Status == "Dead"; } }
        public string DisplayStatus { get { return Closed ? (Fatal ? "Respawned" : "Returned") : Status == "Missing" && ReportedKilled ? "Killed / MIA" : Status; } }
        public Casualty Copy() { var c = (Casualty)MemberwiseClone(); c.Location = Location == null ? null : Location.Copy(); return c; }
    }

    public sealed class Context
    {
        public string Vessel = "", VesselId = "", Part = "", PartTitle = "", PartId = "", Body = "", Situation = "", EVA = "", Quality = "";
        public double UT = -1, Latitude = double.NaN, Longitude = double.NaN, Altitude = double.NaN, Speed = double.NaN, Gee = double.NaN, MET = double.NaN;
        public Context Copy() { return (Context)MemberwiseClone(); }
    }

    public sealed class Ledger
    {
        public readonly List<Casualty> Records = new List<Casualty>();
        public Casualty Open(string name) { return Records.LastOrDefault(r => !r.Closed && r.Name == name); }
        public Casualty Observe(string name, string status, double ut, bool killed = false, bool imported = false)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var r = Open(name);
            if (status == "Available" || status == "Assigned")
            {
                if (r != null) r.ReturnUT = Math.Max(0, ut);
                return r;
            }
            if (status != "Missing" && status != "Dead" && !killed) return null;
            if (r == null)
            {
                r = new Casualty { Name = name, OriginalName = name, UT = imported ? -1 : ut, Imported = imported };
                Records.Add(r);
            }
            if (status == "Dead") r.Status = "Dead";
            if (killed) r.ReportedKilled = true;
            if (r.Fatal && r.DeathUT < 0 && !imported) r.DeathUT = ut;
            return r;
        }
        public void Rename(string oldName, string newName)
        {
            foreach (var r in Records.Where(x => x.Name == oldName)) r.Name = newName;
        }
        // Elapsed time follows the current timeline, not archived alternate timelines.
        // An incident begins with the casualty record, not a later death confirmation or return.
        public static double LatestIncidentUT(IEnumerable<Casualty> records, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return -1;
            return records.Where(r => r != null && r.UT >= 0 && r.UT <= now)
                .Select(r => r.UT).DefaultIfEmpty(-1).Max();
        }
        public static string FormatElapsedDays(double elapsed, int secondsPerDay)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || secondsPerDay <= 0) return "Unknown";
            double days = Math.Floor(Math.Max(0, elapsed) / secondsPerDay);
            return days.ToString("0", CultureInfo.InvariantCulture) + (days == 1 ? " day" : " days");
        }
        // Safety board uses the same dated current-timeline incidents as the window.
        // If no accident has been recorded, count from game UT zero; never invent a
        // date for a historical casualty whose actual date is unknown.
        public static string SafetySignDays(ICollection<Casualty> records, double now, int secondsPerDay)
        {
            if (records == null || double.IsNaN(now) || double.IsInfinity(now) || now < 0 || secondsPerDay <= 0)
                return "--- DAYS";
            double last = LatestIncidentUT(records, now);
            if (last < 0 && records.Count != 0) return "--- DAYS";
            double days = Math.Floor((now - Math.Max(0, last)) / secondsPerDay);
            return days.ToString("0", CultureInfo.InvariantCulture) + " DAYS";
        }
        public static bool IsMemorialLoss(Casualty record)
        { return record != null && (record.Fatal || (!record.Closed && record.Status == "Missing")); }
        public static int ConfidenceRank(string s) { return s == "Confirmed" ? 3 : s == "Probable" ? 2 : s == "Unknown" ? 0 : 1; }
        public static void Cause(Casualty r, string cause, string confidence, string source)
        {
            if (r == null || string.IsNullOrEmpty(cause) || cause == "Unknown") return;
            if (ConfidenceRank(confidence) > ConfidenceRank(r.Confidence)) { r.Cause = cause; r.Confidence = confidence; r.Source = source; }
        }
        // Stock evidence is more useful when a specific failure replaces a
        // generic destruction report at the same confidence level.
        public static int CauseSpecificity(string cause)
        {
            switch (cause)
            {
                case "Overheating": case "EVA overheating":
                case "Excessive G-force (structural failure)": case "Excessive pressure":
                case "Vessel terminated": return 50;
                case "Unloaded vessel terrain impact": return 45;
                case "Water impact": case "EVA water impact": return 40;
                case "Ground impact": case "EVA ground impact": return 35;
                case "Vessel collision": case "EVA collision": return 30;
                case "Aerodynamic breakup": return 25;
                case "Structural failure": return 20;
                case "Collision": case "Left for dead": return 15;
                case "EVA accident — unknown cause": return 12;
                case "Crash": return 10;
                case "Part destroyed": return 5;
                default: return 0;
            }
        }
        public static bool PreferCause(string candidate, string confidence, string existing, string existingConfidence)
        {
            if (string.IsNullOrEmpty(candidate) || candidate == "Unknown") return false;
            int c = ConfidenceRank(confidence), e = ConfidenceRank(existingConfidence);
            // A legacy/custom confirmed cause that isn't one of our stock labels
            // should never be displaced by a new stock hint.
            if (existingConfidence == "Confirmed" && CauseSpecificity(existing) == 0) return false;
            return c > e || (c == e && CauseSpecificity(candidate) > CauseSpecificity(existing));
        }
        public static string EvaCause(string cause, bool eva)
        {
            if (!eva) return cause;
            switch (cause)
            {
                case "Ground impact": case "Unloaded vessel terrain impact": return "EVA ground impact";
                case "Collision": case "Vessel collision": return "EVA collision";
                case "Overheating": return "EVA overheating";
                case "Water impact": return "EVA water impact";
                default: return cause;
            }
        }
        // Joint destruction can also be intentional. Require a positive force;
        // 10 kPa is a heuristic, not a stock aerodynamic failure threshold.
        public static string JointCause(double force, double pressure, bool airborne)
        {
            if (double.IsNaN(force) || double.IsInfinity(force) || force <= 0) return "Unknown";
            return airborne && !double.IsNaN(pressure) && !double.IsInfinity(pressure) && pressure >= 10.0
                ? "Aerodynamic breakup" : "Structural failure";
        }
        public static bool EvidenceInWindow(double eventUT, double nowUT, double realAge)
        {
            return eventUT >= 0 && nowUT >= eventUT && nowUT - eventUT <= 5.0 && realAge >= 0 && realAge <= 5.0;
        }
        public static bool SameContext(Context a, Context b)
        {
            if (a == null || b == null) return true;
            if (!string.IsNullOrEmpty(a.VesselId) && !string.IsNullOrEmpty(b.VesselId) && a.VesselId != b.VesselId) return false;
            return string.IsNullOrEmpty(a.PartId) || string.IsNullOrEmpty(b.PartId) || a.PartId == b.PartId;
        }
        public static bool IsStockEvidence(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            if (source == "Ground collision event" || source == "Tracking Station termination") return true;
            foreach (string name in new[] { "onOverheat", "onOverG", "onOverPressure", "onCrashSplashdown", "onCollision", "onPartFailure", "onPartJointBreak", "onVesselExplodeGroundCollision", "onPartExplodeGroundCollision", "onPartDie", "onCrash" })
                if (source == name || source.StartsWith(name + ":", StringComparison.Ordinal) || source.StartsWith(name + ";", StringComparison.Ordinal) || source.StartsWith(name + " (", StringComparison.Ordinal) || source.StartsWith(name + " | ", StringComparison.Ordinal)) return true;
            return source.StartsWith("Tracking Station termination | ", StringComparison.Ordinal);
        }
        public static string MergeEvidence(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second ?? "";
            if (string.IsNullOrEmpty(second)) return first;
            var lines = first.Split(new[] { " | " }, StringSplitOptions.None).ToList();
            foreach (string item in second.Split(new[] { " | " }, StringSplitOptions.None))
                if (!lines.Contains(item) && lines.Count < 8) lines.Add(item);
            return string.Join(" | ", lines);
        }
        public static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
        public static string Number(double n) { return double.IsNaN(n) || double.IsInfinity(n) ? "" : n.ToString("R", CultureInfo.InvariantCulture); }
    }
}
