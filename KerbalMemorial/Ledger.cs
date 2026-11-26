using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KerbalMemorial
{
    public sealed class Casualty
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "", OriginalName = "", Trait = "", Gender = "", CrewType = "", Level = "";
        public string Status = "Missing", Cause = "Unknown", Confidence = "Unknown", Source = "", Notes = "", History = "";
        public string PortraitPng = "", Inscription = "";
        public double UT = -1, DeathUT = -1, ReturnUT = -1;
        public bool ReportedKilled, Imported, ExternalCause, InscriptionForMissing, InscriptionFromMissingPool;
        public Context Location;
        public bool Closed
        {
            get
            {
                return ReturnUT >= 0;
            }
        }
        public bool Fatal
        {
            get
            {
                return ReportedKilled || Status == "Dead";
            }
        }
        public string DisplayStatus
        {
            get
            {
                return Closed ? (Fatal ? "Respawned" : "Returned") : Status == "Missing" && ReportedKilled ? "Killed / MIA" : Status;
            }
        }
        public Casualty Copy()
        {
            var c = (Casualty)MemberwiseClone();
            c.Location = Location == null ? null : Location.Copy();
            return c;
        }
    }

    public sealed class Context
    {
        public string Vessel = "", VesselId = "", Part = "", PartTitle = "", PartId = "", Body = "", Situation = "", EVA = "", Quality = "";
        public double EvaDepartureUT = -1;
        internal double ObservedRealtime = -1;
        public double UT = -1, Latitude = double.NaN, Longitude = double.NaN, Altitude = double.NaN, Speed = double.NaN, Gee = double.NaN, MET = double.NaN;
        public Context Copy()
        {
            return (Context)MemberwiseClone();
        }
    }

    public sealed class Ledger
    {
        public readonly List<Casualty> Records = new List<Casualty>();
        public Casualty Open(string name)
        {
            return Records.LastOrDefault(r => !r.Closed && r.Name == name);
        }
        public Casualty Observe(string name, string status, double ut, bool killed = false, bool imported = false)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var r = Open(name);
            if (status == "Available" || status == "Assigned")
            {
                if (r != null)
                    r.ReturnUT = Math.Max(0, ut);
                return r;
            }
            if (status != "Missing" && status != "Dead" && !killed)
                return null;
            if (r == null)
            {
                r = new Casualty { Name = name, OriginalName = name, UT = imported ? -1 : ut, Imported = imported };
                Records.Add(r);
            }
            if (status == "Dead")
                r.Status = "Dead";
            if (killed)
                r.ReportedKilled = true;
            if (r.Fatal && r.DeathUT < 0 && !imported)
                r.DeathUT = ut;
            return r;
        }
        public void Rename(string oldName, string newName)
        {
            foreach (var r in Records.Where(x => x.Name == oldName))
                r.Name = newName;
        }
        // Elapsed time follows the current timeline, not archived alternate timelines.
        // An incident begins with the casualty record, not a later death confirmation or return.
        public static double LatestIncidentUT(IEnumerable<Casualty> records, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now))
                return -1;
            return records.Where(r => r != null && r.UT >= 0 && r.UT <= now)
                .Select(r => r.UT).DefaultIfEmpty(-1).Max();
        }
        public static string FormatElapsedDays(double elapsed, int secondsPerDay)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || secondsPerDay <= 0)
                return "Unknown";
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
            if (last < 0 && records.Count != 0)
                return "--- DAYS";
            double days = Math.Floor((now - Math.Max(0, last)) / secondsPerDay);
            return days.ToString("0", CultureInfo.InvariantCulture) + " DAYS";
        }
        public static bool IsMemorialLoss(Casualty record)
        {
            return record != null && (record.Fatal || (!record.Closed && record.Status == "Missing"));
        }
        public static int ConfidenceRank(string s)
        {
            return s == "Confirmed" ? 3 : s == "Probable" ? 2 : s == "Unknown" ? 0 : 1;
        }
        public static void Cause(Casualty r, string cause, string confidence, string source)
        {
            if (r == null || string.IsNullOrEmpty(cause) || cause == "Unknown")
                return;
            if (ConfidenceRank(confidence) > ConfidenceRank(r.Confidence))
            {
                r.Cause = cause;
                r.Confidence = confidence;
                r.Source = source;
            }
        }
        // Only parse a number adjacent to its unit. Do not mistake a part name,
        // timestamp, stage number or unrelated number for temperature or speed.
        public static double ReportMeasurement(string message, bool temperature)
        {
            if (string.IsNullOrEmpty(message))
                return double.NaN;
            string unit = temperature ? @"(?:K|Kelvin)" : @"(?:m/s|m\s*/\s*s|m\s+s-1)";
            var matches = Regex.Matches(message, @"(?<![\w.,+-])(?<value>[+]?(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)\s*" + unit + @"(?![\w/])", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (double.TryParse(match.Groups["value"].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double value) && FinitePositive(value))
                    return value;
            }
            return double.NaN;
        }
        private static bool FinitePositive(double value)
        {
            return value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
        }
        public static double EventMeasurement(string message, double parameter, bool temperature)
        {
            double reported = ReportMeasurement(message, temperature);
            if (FinitePositive(reported))
                return reported;
            return FinitePositive(parameter) ? parameter : double.NaN;
        }
        // Called only for a stock overheating event, not ordinary telemetry.
        public static string ThermalCause(double reported, double internalTemp, double internalLimit, double skinTemp, double skinLimit)
        {
            if (ExtremeHeat(reported, double.NaN) || ExtremeHeat(internalTemp, internalLimit) || ExtremeHeat(skinTemp, skinLimit))
                return "Atomized";
            return "Cooked";
        }
        private static bool ExtremeHeat(double temperature, double limit)
        {
            return FinitePositive(temperature) && temperature >= 10000 && (!FinitePositive(limit) || temperature > limit);
        }
        public static string ImpactCause(string fallback, double impactSpeed, double crashTolerance)
        {
            if (FinitePositive(impactSpeed) && FinitePositive(crashTolerance) && impactSpeed / crashTolerance >= 2.0)
                return "Stomped (1)";
            return fallback;
        }
        public static string DestructionCause(string fallback, double gee, bool impacting)
        {
            if (!impacting && FinitePositive(gee) && gee >= 15.0)
                return "Stomped (2)";
            return fallback;
        }
        public const string BuriedCause = "Buried";
        public const string DrownedCause = "Drowned";
        // Allow half a meter for PQS approximation and normal contact.
        public static bool IsBuried(double partAltitude, double terrainAltitude)
        {
            if (double.IsNaN(partAltitude) || double.IsInfinity(partAltitude) ||
                double.IsNaN(terrainAltitude) || double.IsInfinity(terrainAltitude))
                return false;
            double depth = terrainAltitude - partAltitude;
            return !double.IsInfinity(depth) && depth >= 0.5;
        }
        public static bool IsImpactEvidence(string cause, string source)
        {
            switch (cause)
            {
                case "Stomped (1)":
                case "Crash":
                case "Collision":
                case "Vessel collision":
                case "Ground impact":
                case "Unloaded vessel terrain impact":
                case "Water impact":
                case "EVA ground impact":
                case "EVA collision":
                case "EVA water impact":
                    return true;
            }
            if (string.IsNullOrEmpty(source))
                return false;
            foreach (string entry in source.Split(new[] { " | " }, StringSplitOptions.None))
            {
                foreach (string name in new[] { "onCrash", "onCollision", "onCrashSplashdown", "onPartExplodeGroundCollision", "onVesselExplodeGroundCollision" })
                {
                    if (entry == name || entry.StartsWith(name + ":", StringComparison.Ordinal) || entry.StartsWith(name + ";", StringComparison.Ordinal) || entry.StartsWith(name + " (", StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }
        public const string SpacedCause = "Spaced";
        public static bool IsUnexplainedEvaCause(string cause)
        {
            return string.IsNullOrEmpty(cause) || cause == "Unknown" || cause == "Part destroyed" || cause == "EVA accident — unknown cause" || cause == SpacedCause;
        }
        public static bool FreshDeathContext(Context context, double deathUT)
        {
            return context != null && context.UT >= 0 && deathUT >= 0 && Math.Abs(deathUT - context.UT) <= 5.0;
        }
        public static bool RecentEvaContext(Context context, double deathUT, double realtime)
        {
            if (FreshDeathContext(context, deathUT))
                return true;
            // A high-warp roster scan may span many game seconds. Only trust a
            // recent observation in this running scene, never a persisted clock.
            return context != null && context.EVA == "True" && context.UT >= 0 && context.UT <= deathUT &&
                context.ObservedRealtime >= 0 && realtime >= context.ObservedRealtime && realtime - context.ObservedRealtime <= 2.0;
        }
        public static bool ApplySpaced(Casualty record, int secondsPerDay, double realtime = -1)
        {
            if (record == null || !record.Fatal || record.Imported || record.Closed || record.ExternalCause ||
                !IsUnexplainedEvaCause(record.Cause) || record.Cause == SpacedCause ||
                IsImpactEvidence(record.Cause, record.Source) || !RecentEvaContext(record.Location, record.DeathUT, realtime))
                return false;
            var context = record.Location;
            if (context.EVA != "True" || context.Situation != "ORBITING")
                return false;
            double elapsed = record.DeathUT - context.EvaDepartureUT;
            bool knownDeparture = context.EvaDepartureUT >= 0 && elapsed >= 0 && !double.IsInfinity(elapsed) && !double.IsNaN(elapsed);
            bool recent = knownDeparture && secondsPerDay > 0 && elapsed <= secondsPerDay;
            record.Cause = SpacedCause;
            record.Confidence = recent && FreshDeathContext(context, record.DeathUT) ? "Probable" : "Possible";
            string evidence = "Orbital EVA death; no specific cause identified";
            if (knownDeparture)
                evidence += "; time since hatch departure " + elapsed.ToString("F1", CultureInfo.InvariantCulture) + " s" +
                    (recent ? " (within one game-calendar day)" : "");
            else
                evidence += "; hatch departure time unavailable";
            if (!FreshDeathContext(context, record.DeathUT))
                evidence += "; last observed orbit before time-warped death";
            record.Source = MergeEvidence(record.Source, evidence);
            return true;
        }
        // Use only within the existing matching-part and fresh-evidence checks.
        public static bool PreferEvidence(string candidate, string confidence, string source, string existing, string existingConfidence, string existingSource)
        {
            if (existingConfidence == "Confirmed" && CauseSpecificity(existing) == 0)
                return false;
            // A measured underground destruction is more specific than any
            // ordinary stock cause, regardless of event arrival order.
            if (candidate == BuriedCause || existing == BuriedCause)
                return candidate == BuriedCause && existing != BuriedCause;
            // Destruction while SPLASHED is a specific stock classification.
            // Preserve it even if generic damage reports arrive later.
            if (candidate == DrownedCause || existing == DrownedCause)
                return candidate == DrownedCause && existing != DrownedCause;
            bool impact = IsImpactEvidence(candidate, source);
            bool hadImpact = IsImpactEvidence(existing, existingSource);
            if (candidate == "Stomped (2)" && (impact || hadImpact))
                return false;
            // Spaced is a fallback, never a competing specific failure. A later
            // matched failure report wins even if its confidence is only Possible.
            if (existing == SpacedCause)
                return !IsUnexplainedEvaCause(candidate);
            if (candidate == SpacedCause)
                return existing != SpacedCause && IsUnexplainedEvaCause(existing) && !IsImpactEvidence(existing, existingSource);
            if (existing == "Stomped (2)" && impact && candidate != "Stomped (2)")
                return true;
            if (candidate == "Stomped (1)" && hadImpact && existing != "Stomped (1)" && IsImpactEvidence(existing, ""))
                return true;
            if (existing == "Stomped (1)" && IsImpactEvidence(candidate, "") && candidate != "Stomped (1)")
                return false;
            return PreferCause(candidate, confidence, existing, existingConfidence);
        }
        // Stock evidence is more useful when a specific failure replaces a
        // generic destruction report at the same confidence level.
        public static int CauseSpecificity(string cause)
        {
            switch (cause)
            {
                case BuriedCause:
                    return 110;
                case DrownedCause:
                    return 105;
                case "Atomized":
                    return 100;
                case "Cooked":
                    return 90;
                case "Stomped (1)":
                    return 80;
                case "Stomped (2)":
                    return 60;
                case "Overheating":
                case "EVA overheating":
                case "Excessive G-force (structural failure)":
                case "Excessive pressure":
                case "Vessel terminated":
                    return 50;
                case "Unloaded vessel terrain impact":
                    return 45;
                case "Water impact":
                case "EVA water impact":
                    return 40;
                case "Ground impact":
                case "EVA ground impact":
                    return 35;
                case "Vessel collision":
                case "EVA collision":
                    return 30;
                case "Aerodynamic breakup":
                    return 25;
                case "Structural failure":
                    return 20;
                case "Collision":
                case "Left for dead":
                    return 15;
                case SpacedCause:
                    return 13;
                case "EVA accident — unknown cause":
                    return 12;
                case "Crash":
                    return 10;
                case "Part destroyed":
                    return 5;
                default:
                    return 0;
            }
        }
        public static bool PreferCause(string candidate, string confidence, string existing, string existingConfidence)
        {
            if (string.IsNullOrEmpty(candidate) || candidate == "Unknown")
                return false;
            int c = ConfidenceRank(confidence), e = ConfidenceRank(existingConfidence);
            // A legacy/custom confirmed cause that isn't one of our stock labels
            // should never be displaced by a new stock hint.
            if (existingConfidence == "Confirmed" && CauseSpecificity(existing) == 0)
                return false;
            return c > e || (c == e && CauseSpecificity(candidate) > CauseSpecificity(existing));
        }
        public static string EvaCause(string cause, bool eva)
        {
            if (!eva)
                return cause;
            switch (cause)
            {
                case "Ground impact":
                case "Unloaded vessel terrain impact":
                    return "EVA ground impact";
                case "Collision":
                case "Vessel collision":
                    return "EVA collision";
                case "Overheating":
                    return "EVA overheating";
                case "Water impact":
                    return "EVA water impact";
                default:
                    return cause;
            }
        }
        // Joint destruction can also be intentional. Require a positive force;
        // 10 kPa is a heuristic, not a stock aerodynamic failure threshold.
        public static string JointCause(double force, double pressure, bool airborne)
        {
            if (double.IsNaN(force) || double.IsInfinity(force) || force <= 0)
                return "Unknown";
            return airborne && !double.IsNaN(pressure) && !double.IsInfinity(pressure) && pressure >= 10.0
            ? "Aerodynamic breakup" : "Structural failure";
        }
        public static bool EvidenceInWindow(double eventUT, double nowUT, double realAge)
        {
            return eventUT >= 0 && nowUT >= eventUT && nowUT - eventUT <= 5.0 && realAge >= 0 && realAge <= 5.0;
        }
        public static bool SameContext(Context a, Context b)
        {
            if (a == null || b == null)
                return true;
            if (!string.IsNullOrEmpty(a.VesselId) && !string.IsNullOrEmpty(b.VesselId) && a.VesselId != b.VesselId)
                return false;
            return string.IsNullOrEmpty(a.PartId) || string.IsNullOrEmpty(b.PartId) || a.PartId == b.PartId;
        }
        public static bool IsStockEvidence(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;
            if (source == "Ground collision event" || source == "Tracking Station termination")
                return true;
            foreach (string name in new[] { "onOverheat", "onOverG", "onOverPressure", "onCrashSplashdown", "onCollision", "onPartFailure", "onPartJointBreak", "onVesselExplodeGroundCollision", "onPartExplodeGroundCollision", "onPartDie", "onCrash" })
                if (source == name || source.StartsWith(name + ":", StringComparison.Ordinal) || source.StartsWith(name + ";", StringComparison.Ordinal) || source.StartsWith(name + " (", StringComparison.Ordinal) || source.StartsWith(name + " | ", StringComparison.Ordinal))
                    return true;
            return source.StartsWith("Tracking Station termination | ", StringComparison.Ordinal);
        }
        public static string MergeEvidence(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
                return second ?? "";
            if (string.IsNullOrEmpty(second))
                return first;
            var lines = first.Split(new[] { " | " }, StringSplitOptions.None).ToList();
            foreach (string item in second.Split(new[] { " | " }, StringSplitOptions.None))
                if (!lines.Contains(item) && lines.Count < 8)
                    lines.Add(item);
            return string.Join(" | ", lines);
        }
        public static string Csv(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }
        public static string Number(double n)
        {
            return double.IsNaN(n) || double.IsInfinity(n) ? "" : n.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
