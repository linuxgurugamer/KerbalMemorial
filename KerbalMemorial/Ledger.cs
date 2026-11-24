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
        public string PortraitPng = "";
        public double UT = -1, DeathUT = -1, ReturnUT = -1;
        public bool ReportedKilled, Imported;
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
        public static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
        public static string Number(double n) { return double.IsNaN(n) || double.IsInfinity(n) ? "" : n.ToString("R", CultureInfo.InvariantCulture); }
    }
}
