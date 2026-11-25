using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RS = ProtoCrewMember.RosterStatus;

namespace KerbalMemorial
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR)]
    public sealed class CasualtyTracker : ScenarioModule
    {
        public static CasualtyTracker Instance { get; private set; }
        internal readonly Ledger Live = new Ledger();
        private readonly PortraitCache portraits = new PortraitCache();
        private readonly Dictionary<string, Casualty> archive = new Dictionary<string, Casualty>();
        private readonly Dictionary<string, Context> snapshots = new Dictionary<string, Context>();
        private readonly Dictionary<string, RS> seen = new Dictionary<string, RS>();
        private readonly Dictionary<string, Hint> hints = new Dictionary<string, Hint>();
        private sealed class Hint { public string Cause, Confidence, Source; public float Time; public Context Location; }
        private bool ready, initialized, archiveLoaded, archiveDirty, wasPermanent, subscribed;
        private float nextPoll, nextWrite, nextPortraitPoll, nextInscriptionPoll;
        private string saveFolder;
        private Vessel portraitVessel;
        internal string Error { get; private set; }
        internal static double Now => Planetarium.GetUniversalTime();
        internal static Settings Options => HighLogic.CurrentGame.Parameters.CustomParams<Settings>();
        internal bool Permanent => HighLogic.CurrentGame != null && Options.permanentHistory;
        private bool Active => ready && Instance == this && HighLogic.CurrentGame != null && saveFolder == HighLogic.SaveFolder && !KrashIntegration.IsSimulationActive;
        internal bool IsReady => Active && initialized;
        private static bool Lost(RS status) => status == RS.Dead || status == RS.Missing;
        private static bool Alive(RS status) => status == RS.Available || status == RS.Assigned;

        public override void OnAwake()
        {
            base.OnAwake(); Instance = this;
            if (subscribed) return;
            subscribed = true;
            GameEvents.onKerbalStatusChange.Add(BeforeStatus);
            GameEvents.onKerbalStatusChanged.Add(StatusChanged);
            GameEvents.onKerbalNameChanged.Add(Renamed);
            GameEvents.onCrewKilled.Add(Killed);
            GameEvents.onFlightReady.Add(FlightReady);
            GameEvents.onPartDie.Add(PartDied);
            GameEvents.onPartExplodeGroundCollision.Add(GroundImpact);
            GameEvents.onCrash.Add(Crash);
            GameEvents.onVesselWillDestroy.Add(VesselDestroying);
            GameEvents.onVesselTerminated.Add(Terminated);
            GameEvents.OnCrewmemberLeftForDead.Add(LeftForDead);
        }
        public void OnDestroy()
        {
            if (Active) FlushArchive();
            if (subscribed)
            {
                GameEvents.onKerbalStatusChange.Remove(BeforeStatus);
                GameEvents.onKerbalStatusChanged.Remove(StatusChanged);
                GameEvents.onKerbalNameChanged.Remove(Renamed);
                GameEvents.onCrewKilled.Remove(Killed);
                GameEvents.onFlightReady.Remove(FlightReady);
                GameEvents.onPartDie.Remove(PartDied);
                GameEvents.onPartExplodeGroundCollision.Remove(GroundImpact);
                GameEvents.onCrash.Remove(Crash);
                GameEvents.onVesselWillDestroy.Remove(VesselDestroying);
                GameEvents.onVesselTerminated.Remove(Terminated);
                GameEvents.OnCrewmemberLeftForDead.Remove(LeftForDead);
                subscribed = false;
            }
            portraits.Clear();
            if (Instance == this) Instance = null;
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ready = false; Live.Records.Clear(); Live.Records.AddRange(Persistence.Read(node));
            archive.Clear(); portraitVessel = null; portraits.Read(node); snapshots.Clear(); hints.Clear(); seen.Clear();
            initialized = archiveLoaded = archiveDirty = wasPermanent = false; Error = null;
            bool.TryParse(node.GetValue("initialized"), out initialized);
            saveFolder = HighLogic.SaveFolder; nextPoll = nextWrite = nextPortraitPoll = nextInscriptionPoll = 0; ready = true;
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (Active) { try { Poll(true); FlushArchive(); } catch (Exception e) { Debug.LogWarning("[KerbalMemorial] Save scan: " + e); } }
            node.SetValue("initialized", initialized.ToString(), true);
            Persistence.Write(node, Live.Records);
            portraits.Write(node);
        }
        public void Start()
        {
            // New scenarios may start without a serialized node.
            if (!ready) { saveFolder = HighLogic.SaveFolder; ready = true; }
        }
        public void Update()
        {
            if (!Active) return;
            if (Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 1f;
                try { Poll(); } catch (Exception e) { Debug.LogWarning("[KerbalMemorial] Poll: " + e); }
            }
            if (archiveDirty && Time.realtimeSinceStartup >= nextWrite) FlushArchive();
        }
        private void FlightReady()
        {
            if (!Active) return;
            portraits.BeginFlight();
            portraitVessel = null;
            nextPortraitPoll = 0;
        }
        public void LateUpdate()
        {
            // Independent of the casualty poll and memorial window. Start on the pad,
            // then retry until stock has rendered each crew member's portrait.
            if (!Active || !HighLogic.LoadedSceneIsFlight || Time.realtimeSinceStartup < nextPortraitPoll) return;
            nextPortraitPoll = Time.realtimeSinceStartup + 0.2f;
            try { CaptureActiveCrew(); }
            catch (Exception e) { Debug.LogWarning("[KerbalMemorial] Flight portrait scan: " + e.Message); }
        }
        private void CaptureActiveCrew()
        {
            if (!Active || !HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready) return;
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel != portraitVessel) { portraits.BeginFlight(); portraitVessel = vessel; }
            if (vessel == null || !vessel.loaded) return;
            foreach (var crew in vessel.GetVesselCrew()) portraits.Capture(crew);
        }
        private IEnumerable<ProtoCrewMember> Roster()
        {
            var roster = HighLogic.CurrentGame?.CrewRoster;
            return roster == null ? Enumerable.Empty<ProtoCrewMember>() : roster.Kerbals(RS.Available, RS.Assigned, RS.Missing, RS.Dead);
        }
        private void Poll(bool forceInscriptions = false)
        {
            if (!Active) return;
            EnsureArchive();
            if (FlightGlobals.Vessels != null)
                foreach (var v in FlightGlobals.Vessels) CaptureVessel(v, "Last known");
            foreach (var crew in Roster())
            {
                if (crew == null) continue;
                var status = crew.rosterStatus;
                bool known = seen.TryGetValue(crew.name, out RS old);
                if (Lost(status))
                {
                    var open = Live.Open(crew.name);
                    if (open != null && (open.Status != status.ToString() && status == RS.Dead)) Record(crew, status, false, false, "Roster scan");
                    else if (open == null && ((known && !Lost(old)) || (!known && Options.importExisting))) Record(crew, status, false, !known, "Roster scan");
                }
                else if (Alive(status) && ((known && Lost(old)) || (!known && Live.Open(crew.name) != null))) Record(crew, status, false, false, "Roster scan");
                if (status == RS.Available && !Lost(status)) snapshots.Remove(crew.name);
                seen[crew.name] = status;
            }
            initialized = true;
            if (forceInscriptions || Time.realtimeSinceStartup >= nextInscriptionPoll)
            {
                nextInscriptionPoll = Time.realtimeSinceStartup + 10f;
                BackfillInscriptions();
            }
            foreach (var name in hints.Where(h => Time.realtimeSinceStartup - h.Value.Time > 5f).Select(h => h.Key).ToArray()) hints.Remove(name);
        }
        private void BeforeStatus(ProtoCrewMember c, RS oldStatus, RS newStatus)
        {
            if (Active && c != null && Lost(newStatus) && !Lost(oldStatus))
            { CaptureCurrent(c.name); }
        }
        private void StatusChanged(ProtoCrewMember c, RS oldStatus, RS newStatus)
        {
            if (!Active || c == null || oldStatus == newStatus) return;
            if (Lost(newStatus) || (Alive(newStatus) && Lost(oldStatus))) Record(c, newStatus, false, false, "Roster status event");
            seen[c.name] = newStatus;
        }
        private void Renamed(ProtoCrewMember c, string oldName, string newName)
        {
            if (!Active) return;
            Live.Rename(oldName, newName);
            portraits.Rename(oldName, newName);
            MoveKey(snapshots, oldName, newName); MoveKey(seen, oldName, newName); MoveKey(hints, oldName, newName);
            EnsureArchive();
            if (Permanent && archiveLoaded) { foreach (var r in archive.Values.Where(r => r.Name == oldName)) r.Name = newName; archiveDirty = true; }
            foreach (var r in Live.Records.Where(r => r.Name == newName)) Changed(r);
        }
        private static void MoveKey<T>(Dictionary<string,T> d, string from, string to)
        { if (d.TryGetValue(from, out T value)) { d.Remove(from); d[to] = value; } }
        private void Killed(EventReport report)
        {
            if (!Active || report == null) return;
            // Do not infer a casualty from all crew in report.origin: a report can describe one person only.
            var crew = Roster().FirstOrDefault(c => c != null && c.name == report.sender);
            if (crew == null) crew = Roster().FirstOrDefault(c => c != null && c.name == report.other);
            if (crew == null) return;
            if (report.origin != null && (report.origin.protoModuleCrew.Contains(crew) ||
                (snapshots.TryGetValue(crew.name, out Context cached) && cached.PartId == report.origin.flightID.ToString())))
                Store(crew.name, FromVessel(report.origin.vessel, report.origin, "Death event"));
            var r = Record(crew, Lost(crew.rosterStatus) ? crew.rosterStatus : RS.Missing, true, false, "Crew killed event");
            if (r != null && r.Confidence == "Unknown" && !string.IsNullOrEmpty(report.msg)) { r.Source = "Crew killed event: " + report.msg; Changed(r); }
        }
        private void LeftForDead(ProtoCrewMember crew, int count)
        { if (Active && crew != null) { SetHint(crew.name, "Left for dead", "Probable", "Left-for-dead event", null); if (Lost(crew.rosterStatus)) Record(crew, crew.rosterStatus, false, false, "Left-for-dead event"); } }
        private void PartDied(Part p) { PartHint(p, "Part destroyed", "Probable", "Part death event"); }
        private void GroundImpact(Part p) { PartHint(p, "Ground impact", "Confirmed", "Ground collision event"); }
        private void Crash(EventReport r) { if (r != null) PartHint(r.origin, "Crash", "Probable", "Crash event"); }
        private void PartHint(Part p, string cause, string confidence, string source)
        {
            if (!Active || p == null) return;
            var ctx = FromVessel(p.vessel, p, "Destruction event");
            var names = new HashSet<string>(p.protoModuleCrew.Where(c => c != null).Select(c => c.name));
            foreach (var pair in snapshots)
                if (pair.Value.PartId == p.flightID.ToString() && pair.Value.VesselId == ctx.VesselId && Math.Abs(Now - pair.Value.UT) <= 5) names.Add(pair.Key);
            foreach (var name in names) SetHint(name, cause, confidence, source, ctx);
        }
        private void VesselDestroying(Vessel v) { if (Active) CaptureVessel(v, "Vessel destruction event"); }
        private void Terminated(ProtoVessel pv)
        {
            if (!Active || pv == null) return;
            var ctx = pv.vesselRef != null ? FromVessel(pv.vesselRef, null, "Termination event") : new Context { Vessel = pv.vesselName, VesselId = pv.vesselID.ToString(), Situation = pv.situation.ToString(), UT = Now, Quality = "Termination event" };
            var names = new HashSet<string>(pv.GetVesselCrew().Where(c => c != null).Select(c => c.name));
            foreach (var p in snapshots) if (p.Value.VesselId == ctx.VesselId && Math.Abs(Now - p.Value.UT) <= 5) names.Add(p.Key);
            foreach (var name in names) SetHint(name, "Vessel terminated", "Confirmed", "Tracking Station termination", ctx);
        }
        private void SetHint(string name, string cause, string confidence, string source, Context ctx)
        {
            if (!hints.TryGetValue(name, out Hint previous) || Time.realtimeSinceStartup - previous.Time > 5f || Ledger.ConfidenceRank(confidence) > Ledger.ConfidenceRank(previous.Confidence))
                hints[name] = new Hint { Cause = cause, Confidence = confidence, Source = source, Time = Time.realtimeSinceStartup, Location = ctx };
            var r = Live.Open(name);
            if (r != null && r.UT >= 0 && Math.Abs(Now - r.UT) <= 5)
            { ApplyHint(r); Changed(r); }
        }
        private void ApplyHint(Casualty r)
        {
            if (hints.TryGetValue(r.Name, out Hint h) && Time.realtimeSinceStartup - h.Time <= 5f)
            {
                Ledger.Cause(r, h.Cause, h.Confidence, h.Source);
                if (h.Location != null && (r.Location == null || r.Location.Quality == "Last known")) r.Location = h.Location.Copy();
            }
        }
        private void Store(string name, Context ctx) { if (ctx != null && !string.IsNullOrEmpty(name)) snapshots[name] = ctx; }
        private static Context FromVessel(Vessel v, Part p, string quality)
        {
            var c = new Context { UT = Now, Quality = quality };
            if (v != null)
            {
                c.Vessel = v.vesselName; c.VesselId = v.id.ToString(); c.Body = v.mainBody == null ? "" : v.mainBody.bodyName;
                c.Latitude = v.latitude; c.Longitude = v.longitude; c.Altitude = v.altitude; c.Speed = v.srfSpeed;
                c.Gee = v.loaded ? v.geeForce : double.NaN; c.MET = v.missionTime; c.Situation = v.situation.ToString(); c.EVA = v.isEVA.ToString();
            }
            if (p != null) { c.Part = p.partInfo == null ? p.name : p.partInfo.name; c.PartTitle = p.partInfo == null ? p.name : p.partInfo.title; c.PartId = p.flightID.ToString(); }
            return c;
        }
        private void CaptureVessel(Vessel v, string quality)
        {
            if (v == null) return;
            if (v.loaded)
            {
                foreach (var p in v.parts) if (p != null)
                    foreach (var c in p.protoModuleCrew) if (c != null && !Lost(c.rosterStatus)) Store(c.name, FromVessel(v, p, quality));
            }
            else if (v.protoVessel != null)
            {
                foreach (var p in v.protoVessel.protoPartSnapshots)
                    foreach (var c in p.protoModuleCrew) if (c != null && !Lost(c.rosterStatus))
                    { var ctx = FromVessel(v, null, quality); ctx.Part = p.partName; ctx.PartTitle = p.partInfo == null ? p.partName : p.partInfo.title; ctx.PartId = p.flightID.ToString(); Store(c.name, ctx); }
            }
        }
        private void CaptureCurrent(string name)
        {
            if (FlightGlobals.Vessels == null) return;
            foreach (var v in FlightGlobals.Vessels)
            {
                if (v == null) continue;
                if (v.loaded)
                {
                    foreach (var p in v.parts) if (p != null && p.protoModuleCrew.Any(c => c != null && c.name == name)) { Store(name, FromVessel(v, p, "Status event")); return; }
                }
                else if (v.protoVessel != null)
                {
                    foreach (var p in v.protoVessel.protoPartSnapshots) if (p.protoModuleCrew.Any(c => c != null && c.name == name))
                    { var ctx = FromVessel(v, null, "Status event (unloaded)"); ctx.Part = p.partName; ctx.PartTitle = p.partInfo == null ? p.partName : p.partInfo.title; ctx.PartId = p.flightID.ToString(); Store(name, ctx); return; }
                }
            }
        }
        private Casualty Record(ProtoCrewMember crew, RS status, bool killed, bool imported, string source)
        {
            if (!Active) return null;
            var old = Live.Open(crew.name); bool wasFatal = old != null && old.Fatal;
            var r = Live.Observe(crew.name, status.ToString(), Now, killed, imported);
            if (r == null) return null;
            if (!r.Closed)
            {
                if (string.IsNullOrEmpty(r.PortraitPng))
                { r.PortraitPng = portraits.Get(crew.name); }
                r.Trait = crew.trait; r.Gender = crew.gender.ToString(); r.CrewType = crew.type.ToString(); r.Level = crew.experienceLevel.ToString();
                if (r.Source.Length == 0) r.Source = source;
                if (r.Location == null && !imported && snapshots.TryGetValue(crew.name, out Context ctx)) r.Location = ctx.Copy();
                if (!imported) ApplyHint(r);
                r.History = History(crew);
                // Historical imports are handled together by the poll's backfill.
                // Newly reported fatalities receive an inscription immediately.
                if (r.Fatal && !imported && string.IsNullOrWhiteSpace(r.Inscription))
                {
                    EnsureArchive();
                    Inscriptions.Assign(r, () =>
                    {
                        if (Permanent && archive.TryGetValue(r.Id, out Casualty previous) && !string.IsNullOrWhiteSpace(previous.Inscription))
                            return previous.Inscription;
                        return Inscriptions.Pick(ReadInscriptionPool());
                    });
                }
                if (!imported && Options.notifications && (old == null || (!wasFatal && r.Fatal))) ScreenMessages.PostScreenMessage(crew.name + (r.Fatal ? " has died." : " is missing."), 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            else { snapshots.Remove(crew.name); hints.Remove(crew.name); }
            Changed(r);
            return r;
        }
        private static List<string> ReadInscriptionPool()
        {
            return Inscriptions.ReadPool(System.IO.Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData", "KerbalMemorial", "PluginData", "Inscriptions"),
                message => Debug.LogWarning("[KerbalMemorial] " + message));
        }
        private void BackfillInscriptions()
        {
            EnsureArchive();
            // Read once per pass, only if at least one fatality needs a message.
            List<string> pool = null;
            Func<string> pick = () => Inscriptions.Pick(pool ?? (pool = ReadInscriptionPool()));
            bool includeArchive = Permanent && archiveLoaded;
            var liveIds = new HashSet<string>();
            foreach (var record in Live.Records)
            {
                Casualty archived = null;
                if (includeArchive) { liveIds.Add(record.Id); archive.TryGetValue(record.Id, out archived); }
                if (Inscriptions.FillPair(record, archived, pick)) archiveDirty = true;
            }
            if (includeArchive)
                foreach (var record in archive.Values)
                    if (!liveIds.Contains(record.Id) && Inscriptions.Assign(record, pick)) archiveDirty = true;
            if (includeArchive && archiveDirty) FlushArchive();
        }
        private static string History(ProtoCrewMember crew)
        {
            var node = new ConfigNode("CAREER_LOG");
            if (crew.careerLog != null) crew.careerLog.Save(node);
            var flight = node.AddNode("FLIGHT_LOG"); if (crew.flightLog != null) crew.flightLog.Save(flight);
            return node.ToString();
        }
        internal bool Report(ProtoCrewMember crew, string cause, string source)
        {
            if (!Active || crew == null || string.IsNullOrWhiteSpace(cause)) return false;
            CaptureCurrent(crew.name);
            var r = Record(crew, Lost(crew.rosterStatus) ? crew.rosterStatus : RS.Missing, true, false, source);
            if (r == null) return false;
            r.Cause = cause; r.Confidence = "Confirmed"; r.Source = source; Changed(r); return true;
        }
        private void EnsureArchive()
        {
            if (KrashIntegration.IsSimulationActive) return;
            if (!Permanent) { wasPermanent = false; return; }
            if (!archiveLoaded)
            {
                try { foreach (var r in Persistence.LoadArchive()) archive[r.Id] = r; archiveLoaded = true; Error = null; }
                catch (Exception e) { Error = "Cannot read casualty archive: " + e.Message; return; }
            }
            if (!wasPermanent)
            {
                wasPermanent = true;
                foreach (var r in Live.Records) if (!archive.ContainsKey(r.Id)) { archive[r.Id] = r.Copy(); archiveDirty = true; }
            }
        }
        private void Changed(Casualty r)
        {
            EnsureArchive();
            if (!Permanent || !archiveLoaded) return;
            var copy = r.Copy();
            if (archive.TryGetValue(r.Id, out Casualty previous))
            {
                if (previous.Closed && !copy.Closed) copy.ReturnUT = previous.ReturnUT;
                if (string.IsNullOrEmpty(copy.PortraitPng)) copy.PortraitPng = previous.PortraitPng;
                if (string.IsNullOrEmpty(copy.Inscription)) copy.Inscription = previous.Inscription;
            }
            archive[r.Id] = copy; archiveDirty = true;
            FlushArchive(); // persist immediately before a user can revert or quickload
        }
        private void FlushArchive()
        {
            if (!archiveLoaded || !archiveDirty || saveFolder != HighLogic.SaveFolder || KrashIntegration.IsSimulationActive) return;
            try { Persistence.SaveArchive(archive.Values); archiveDirty = false; Error = null; }
            catch (Exception e) { Error = "Cannot save casualty archive: " + e.Message; Debug.LogWarning("[KerbalMemorial] " + Error); }
            nextWrite = Time.realtimeSinceStartup + 5f;
        }
        internal List<Casualty> VisibleRecords()
        {
            EnsureArchive(); var result = Live.Records.ToList();
            if (Permanent) { var ids = new HashSet<string>(result.Select(r => r.Id)); result.AddRange(archive.Values.Where(r => !ids.Contains(r.Id))); }
            return result.OrderByDescending(r => r.UT).ThenBy(r => r.Name).ToList();
        }
        internal bool IsLive(Casualty r) => Live.Records.Any(x => x.Id == r.Id);
        internal void EditNote(Casualty r, string note)
        { if (KrashIntegration.IsSimulationActive || r.Notes == note) return; r.Notes = note; if (IsLive(r)) Changed(r); else if (Permanent) { archiveDirty = true; FlushArchive(); } }
    }
    public static class MemorialAPI
    {
        public const int Version = 1;
        // Report only actual fatalities. This does not kill or otherwise alter the crew member.
        public static bool ReportDeath(ProtoCrewMember crew, string cause, string sourceMod)
        { return CasualtyTracker.Instance != null && CasualtyTracker.Instance.Report(crew, cause, sourceMod ?? "External mod"); }
    }
}
