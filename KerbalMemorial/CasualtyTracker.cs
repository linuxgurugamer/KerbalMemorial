using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RS = ProtoCrewMember.RosterStatus;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR)]
    public sealed class CasualtyTracker : ScenarioModule
    {
        public static CasualtyTracker Instance
        {
            get;
            private set;
        }
        internal readonly Ledger Live = new Ledger();
        internal FatalityPlaqueQueue DeathPlaques
        {
            get;
            private set;
        }
        = new FatalityPlaqueQueue();
        internal bool CanPresentDeathPlaques
        {
            get
            {
                if (!Active)
                    return false;
                if (Options.showDeathPlaques)
                    return true;
                DeathPlaques.Clear();
                return false;
            }
        }
        private InscriptionCycles inscriptionCycles = new InscriptionCycles();
        private InscriptionCycles archiveCycles = new InscriptionCycles();
        private readonly PortraitCache portraits = new PortraitCache();
        private readonly Dictionary<string, Casualty> archive = new Dictionary<string, Casualty>();
        private readonly Dictionary<string, Context> snapshots = new Dictionary<string, Context>();
        private readonly Dictionary<string, RS> seen = new Dictionary<string, RS>();
        private readonly Dictionary<string, Hint> hints = new Dictionary<string, Hint>();
        private sealed class Hint
        {
            public string Cause, Confidence, Source;
            public float Time;
            public double UT;
            public Context Location;
        }
        private bool ready, initialized, archiveLoaded, archiveDirty, wasPermanent, subscribed, archiveCycleAdopted;
        private float nextPoll, nextWrite, nextPortraitPoll, nextInscriptionPoll;
        private string saveFolder;
        private Vessel portraitVessel;
        internal string Error
        {
            get;
            private set;
        }
        internal static double Now => Planetarium.GetUniversalTime();
        internal static Settings Options => HighLogic.CurrentGame.Parameters.CustomParams<Settings>();
        internal bool Permanent => HighLogic.CurrentGame != null && Options.permanentHistory;
        private bool Active => ready && Instance == this && HighLogic.CurrentGame != null && saveFolder == HighLogic.SaveFolder && !KrashIntegration.IsSimulationActive;
        internal bool IsReady => Active && initialized;
        private static bool Lost(RS status) => status == RS.Dead || status == RS.Missing;
        private static bool Alive(RS status) => status == RS.Available || status == RS.Assigned;

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            if (subscribed)
                return;
            subscribed = true;
            GameEvents.onKerbalStatusChange.Add(BeforeStatus);
            GameEvents.onKerbalStatusChanged.Add(StatusChanged);
            GameEvents.onKerbalNameChanged.Add(Renamed);
            GameEvents.onCrewKilled.Add(Killed);
            GameEvents.onFlightReady.Add(FlightReady);
            GameEvents.onPartDie.Add(PartDied);
            GameEvents.onPartExplodeGroundCollision.Add(GroundImpact);
            GameEvents.onCrash.Add(Crash);
            GameEvents.onOverheat.Add(Overheat);
            GameEvents.onOverG.Add(OverG);
            GameEvents.onOverPressure.Add(OverPressure);
            GameEvents.onCollision.Add(Collision);
            GameEvents.onCrashSplashdown.Add(Splashdown);
            GameEvents.onPartFailure.Add(PartFailure);
            GameEvents.onPartJointBreak.Add(JointBreak);
            GameEvents.onVesselExplodeGroundCollision.Add(VesselGroundImpact);
            GameEvents.onVesselWillDestroy.Add(VesselDestroying);
            GameEvents.onVesselTerminated.Add(Terminated);
            GameEvents.OnCrewmemberLeftForDead.Add(LeftForDead);
        }
        public void OnDestroy()
        {
            if (Active)
                FlushArchive();
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
                GameEvents.onOverheat.Remove(Overheat);
                GameEvents.onOverG.Remove(OverG);
                GameEvents.onOverPressure.Remove(OverPressure);
                GameEvents.onCollision.Remove(Collision);
                GameEvents.onCrashSplashdown.Remove(Splashdown);
                GameEvents.onPartFailure.Remove(PartFailure);
                GameEvents.onPartJointBreak.Remove(JointBreak);
                GameEvents.onVesselExplodeGroundCollision.Remove(VesselGroundImpact);
                GameEvents.onVesselWillDestroy.Remove(VesselDestroying);
                GameEvents.onVesselTerminated.Remove(Terminated);
                GameEvents.OnCrewmemberLeftForDead.Remove(LeftForDead);
                subscribed = false;
            }
            portraits.Clear();
            if (Instance == this)
                Instance = null;
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ready = false;
            DeathPlaques = new FatalityPlaqueQueue();
            Live.Records.Clear();
            Live.Records.AddRange(Persistence.Read(node));
            inscriptionCycles = Persistence.ReadCycles(node);
            archiveCycles = new InscriptionCycles();
            archive.Clear();
            portraitVessel = null;
            portraits.Read(node);
            snapshots.Clear();
            hints.Clear();
            seen.Clear();
            initialized = archiveLoaded = archiveDirty = wasPermanent = archiveCycleAdopted = false;
            Error = null;
            bool.TryParse(node.GetValue("initialized"), out initialized);
            saveFolder = HighLogic.SaveFolder;
            nextPoll = nextWrite = nextPortraitPoll = nextInscriptionPoll = 0;
            ready = true;
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (Active)
            {
                try
                {
                    Poll(true);
                    FlushArchive();
                }
                catch (Exception e)
                {
                    Log.Warning("[KerbalMemorial] Save scan: " + e);
                }
            }
            node.SetValue("initialized", initialized.ToString(), true);
            Persistence.Write(node, Live.Records);
            Persistence.WriteCycles(node, inscriptionCycles);
            portraits.Write(node);
        }
        public void Start()
        {
            // New scenarios may start without a serialized node.
            if (!ready)
            {
                saveFolder = HighLogic.SaveFolder;
                ready = true;
            }
        }
        public void Update()
        {
            if (!Active)
                return;
            if (Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 1f;
                try
                {
                    Poll();
                }
                catch (Exception e)
                {
                    Log.Warning("[KerbalMemorial] Poll: " + e);
                }
            }
            if (archiveDirty && Time.realtimeSinceStartup >= nextWrite)
                FlushArchive();
        }
        private void FlightReady()
        {
            if (!Active)
                return;
            portraits.BeginFlight();
            portraitVessel = null;
            nextPortraitPoll = 0;
        }
        public void LateUpdate()
        {
            // Independent of the casualty poll and memorial window. Start on the pad,
            // then retry until stock has rendered each crew member's portrait.
            if (!Active || !HighLogic.LoadedSceneIsFlight || Time.realtimeSinceStartup < nextPortraitPoll)
                return;
            nextPortraitPoll = Time.realtimeSinceStartup + 0.2f;
            try
            {
                CaptureActiveCrew();
            }
            catch (Exception e)
            {
                Log.Warning("[KerbalMemorial] Flight portrait scan: " + e.Message);
            }
        }
        private void CaptureActiveCrew()
        {
            if (!Active || !HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
                return;
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel != portraitVessel)
            {
                portraits.BeginFlight();
                portraitVessel = vessel;
            }
            if (vessel == null || !vessel.loaded)
                return;
            foreach (var crew in vessel.GetVesselCrew())
                portraits.Capture(crew);
        }
        private IEnumerable<ProtoCrewMember> Roster()
        {
            var roster = HighLogic.CurrentGame?.CrewRoster;
            return roster == null ? Enumerable.Empty<ProtoCrewMember>() : roster.Kerbals(RS.Available, RS.Assigned, RS.Missing, RS.Dead);
        }
        private void Poll(bool forceInscriptions = false)
        {
            if (!Active)
                return;
            EnsureArchive();
            if (FlightGlobals.Vessels != null)
                foreach (var v in FlightGlobals.Vessels)
                    CaptureVessel(v, "Last known");
            foreach (var crew in Roster())
            {
                if (crew == null)
                    continue;
                var status = crew.rosterStatus;
                bool known = seen.TryGetValue(crew.name, out RS old);
                if (Lost(status))
                {
                    var open = Live.Open(crew.name);
                    if (open != null && (open.Status != status.ToString() && status == RS.Dead))
                        Record(crew, status, false, false, "Roster scan");
                    else if (open == null && ((known && !Lost(old)) || (!known && Options.importExisting)))
                        Record(crew, status, false, !known, "Roster scan");
                }
                else if (Alive(status) && ((known && Lost(old)) || (!known && Live.Open(crew.name) != null)))
                    Record(crew, status, false, false, "Roster scan");
                if (status == RS.Available)
                {
                    snapshots.Remove(crew.name);
                    hints.Remove(crew.name);
                }
                seen[crew.name] = status;
            }
            initialized = true;
            RefreshInscriptions(forceInscriptions);
            foreach (var name in hints.Where(h => !Fresh(h.Value)).Select(h => h.Key).ToArray())
                hints.Remove(name);
        }
        private void BeforeStatus(ProtoCrewMember c, RS oldStatus, RS newStatus)
        {
            if (Active && c != null && Lost(newStatus) && !Lost(oldStatus))
            {
                CaptureCurrent(c.name);
            }
        }
        private void StatusChanged(ProtoCrewMember c, RS oldStatus, RS newStatus)
        {
            if (!Active || c == null || oldStatus == newStatus)
                return;
            if (Lost(newStatus) || (Alive(newStatus) && Lost(oldStatus)))
                Record(c, newStatus, false, false, "Roster status event");
            if (newStatus == RS.Available)
            {
                hints.Remove(c.name);
                snapshots.Remove(c.name);
            }
            seen[c.name] = newStatus;
        }
        private void Renamed(ProtoCrewMember c, string oldName, string newName)
        {
            if (!Active)
                return;
            Live.Rename(oldName, newName);
            portraits.Rename(oldName, newName);
            MoveKey(snapshots, oldName, newName);
            MoveKey(seen, oldName, newName);
            MoveKey(hints, oldName, newName);
            EnsureArchive();
            if (Permanent && archiveLoaded)
            {
                foreach (var r in archive.Values.Where(r => r.Name == oldName))
                    r.Name = newName;
                archiveDirty = true;
            }
            foreach (var r in Live.Records.Where(r => r.Name == newName))
                Changed(r);
        }
        private static void MoveKey<T>(Dictionary<string,T> d, string from, string to)
        {
            if (d.TryGetValue(from, out T value))
            {
                d.Remove(from);
                d[to] = value;
            }
        }
        private void Killed(EventReport report)
        {
            if (!Active || report == null)
                return;
            // Do not infer a casualty from all crew in report.origin: a report can describe one person only.
            var crew = Roster().FirstOrDefault(c => c != null && c.name == report.sender);
            if (crew == null)
                crew = Roster().FirstOrDefault(c => c != null && c.name == report.other);
            if (crew == null)
                return;
            if (report.origin != null && (report.origin.protoModuleCrew.Contains(crew) ||
                (snapshots.TryGetValue(crew.name, out Context cached) && cached.PartId == report.origin.flightID.ToString())))
                Store(crew.name, FromVessel(report.origin.vessel, report.origin, "Death event"));
            var r = Record(crew, Lost(crew.rosterStatus) ? crew.rosterStatus : RS.Missing, true, false, "Crew killed event");
            if (r != null && r.Confidence == "Unknown" && !string.IsNullOrEmpty(report.msg))
            {
                r.Source = "Crew killed event: " + report.msg;
                Changed(r);
            }
        }
        private void LeftForDead(ProtoCrewMember crew, int count)
        {
            if (Active && crew != null)
            {
                SetHint(crew.name, "Left for dead", "Probable", "Left-for-dead event", null);
                if (Lost(crew.rosterStatus))
                    Record(crew, crew.rosterStatus, false, false, "Left-for-dead event");
            }
        }
        private void PartDied(Part p)
        {
            PartHint(p, "Part destroyed", "Probable", "onPartDie");
        }
        private void GroundImpact(Part p)
        {
            PartHint(p, "Ground impact", "Confirmed", "onPartExplodeGroundCollision");
        }
        private void Crash(EventReport r)
        {
            if (r != null)
                PartHint(r.origin, "Crash", "Probable", ReportEvidence("onCrash", r));
        }
        private static string ReportEvidence(string name, EventReport report)
        {
            return name + (string.IsNullOrEmpty(report.msg) ? "" : ": " + report.msg);
        }
        private void Overheat(EventReport r)
        {
            if (!Active || r == null || r.origin == null)
                return;
            var p = r.origin;
            PartHint(p, "Overheating", "Confirmed", ReportEvidence("onOverheat", r) +
                "; internal " + p.temperature.ToString("F1") + "/" + p.maxTemp.ToString("F1") + " K, skin " +
                p.skinTemperature.ToString("F1") + "/" + p.skinMaxTemp.ToString("F1") + " K");
        }
        private void OverG(EventReport r)
        {
            if (!Active || r == null || r.origin == null)
                return;
            var p = r.origin;
            PartHint(p, "Excessive G-force (structural failure)", "Confirmed", ReportEvidence("onOverG", r) +
                "; part tolerance " + p.gTolerance.ToString("F1") + " g" +
                (p.vessel == null ? "" : ", vessel acceleration " + p.vessel.geeForce.ToString("F1") + " g"));
        }
        private void OverPressure(EventReport r)
        {
            if (!Active || r == null || r.origin == null)
                return;
            PartHint(r.origin, "Excessive pressure", "Confirmed", ReportEvidence("onOverPressure", r));
        }
        private void Splashdown(EventReport r)
        {
            if (!Active || r == null)
                return;
            PartHint(r.origin, "Water impact", "Probable", ReportEvidence("onCrashSplashdown", r));
        }
        private void Collision(EventReport r)
        {
            if (!Active || r == null || r.origin == null)
                return;
            // EventReport.other is a name, not a Part or vessel ID. Do not claim
            // a vessel collision unless the name identifies one other loaded vessel.
            string other = r.other ?? "";
            Vessel origin = r.origin.vessel;
            var matches = new HashSet<Vessel>();
            if (origin != null && !string.IsNullOrWhiteSpace(other) && FlightGlobals.Vessels != null)
                foreach (var v in FlightGlobals.Vessels)
                {
                    if (v == null || !v.loaded)
                        continue;
                    if (string.Equals(v.vesselName, other, StringComparison.OrdinalIgnoreCase) ||
                        v.parts.Any(p => p != null && (string.Equals(p.name, other, StringComparison.OrdinalIgnoreCase) ||
                        (p.partInfo != null && (string.Equals(p.partInfo.name, other, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.partInfo.title, other, StringComparison.OrdinalIgnoreCase))))))
                        matches.Add(v);
                }
            bool vesselCollision = origin != null && matches.Count == 1 && !matches.Contains(origin);
            PartHint(r.origin, vesselCollision ? "Vessel collision" : "Collision", "Probable",
                ReportEvidence("onCollision", r) + (other.Length == 0 ? "" : "; other: " + other));
        }
        private void PartFailure(Part p)
        {
            PartHint(p, "Structural failure", "Probable", "onPartFailure");
        }
        private void JointBreak(PartJoint joint, float force)
        {
            // This stock event also fires on ordinary joint destruction. A
            // positive finite break force is required; no evidence for staging alone.
            if (!Active || joint == null || float.IsNaN(force) || float.IsInfinity(force) || force <= 0f)
                return;
            var child = joint.Child;
            var parent = joint.Parent;
            JointHint(child, force);
            if (parent != child)
                JointHint(parent, force);
        }
        private void JointHint(Part p, float force)
        {
            if (p == null || p.vessel == null || !p.vessel.loaded)
                return;
            var situation = p.vessel.situation;
            bool airborne = situation == Vessel.Situations.FLYING || situation == Vessel.Situations.SUB_ORBITAL;
            string cause = Ledger.JointCause(force, p.dynamicPressurekPa, airborne);
            if (cause == "Unknown")
                return;
            PartHint(p, cause, "Probable", "onPartJointBreak; break force " + force.ToString("F1") +
                ", dynamic pressure " + p.dynamicPressurekPa.ToString("F1") + " kPa");
        }
        private void VesselGroundImpact(Vessel v)
        {
            if (!Active || v == null)
                return;
            string cause = v.loaded ? "Ground impact" : "Unloaded vessel terrain impact";
            var ctx = FromVessel(v, null, "Vessel ground collision event");
            // Preserve each occupant's part information, including proto parts.
            CaptureVessel(v, "Vessel ground collision event");
            var names = new HashSet<string>();
            if (v.loaded)
            {
                foreach (var crew in v.GetVesselCrew())
                    if (crew != null)
                        names.Add(crew.name);
            }
            else if (v.protoVessel != null)
            {
                foreach (var part in v.protoVessel.protoPartSnapshots)
                    foreach (var crew in part.protoModuleCrew)
                        if (crew != null)
                        {
                            names.Add(crew.name);
                            var location = FromVessel(v, null, "Unloaded vessel ground collision event");
                            location.PartId = part.flightID.ToString();
                            location.Part = part.partName;
                            location.PartTitle = part.partInfo == null ? part.partName : part.partInfo.title;
                            Store(crew.name, location);
                        }
            }
            foreach (var pair in snapshots)
                if (pair.Value.VesselId == ctx.VesselId && Math.Abs(Now - pair.Value.UT) <= 5)
                    names.Add(pair.Key);
            foreach (var name in names)
            {
                Context location;
                if (!snapshots.TryGetValue(name, out location) || location.VesselId != ctx.VesselId)
                    location = ctx;
                SetHint(name, cause, "Confirmed", "onVesselExplodeGroundCollision" + (v.loaded ? " (loaded)" : " (unloaded)"), location);
            }
        }
        private void PartHint(Part p, string cause, string confidence, string source)
        {
            if (!Active || p == null || p.flightID == 0)
                return;
            var ctx = FromVessel(p.vessel, p, "Destruction event");
            var names = new HashSet<string>(p.protoModuleCrew.Where(c => c != null).Select(c => c.name));
            if (p.vessel != null)
                foreach (var crew in p.protoModuleCrew)
                    if (crew != null && !Lost(crew.rosterStatus))
                        Store(crew.name, ctx);
            foreach (var pair in snapshots)
                if (pair.Value.PartId == ctx.PartId && (string.IsNullOrEmpty(ctx.VesselId) || pair.Value.VesselId == ctx.VesselId) && Math.Abs(Now - pair.Value.UT) <= 5)
                    names.Add(pair.Key);
            foreach (var name in names)
            {
                // A late event may have lost its vessel reference; retain the
                // matching crew snapshot instead of losing vessel/EVA context.
                Context location = ctx;
                if (string.IsNullOrEmpty(ctx.VesselId) && snapshots.TryGetValue(name, out Context cached) && cached.PartId == ctx.PartId)
                    location = cached;
                SetHint(name, cause, confidence, source, location);
            }
        }
        private void VesselDestroying(Vessel v)
        {
            if (Active)
                CaptureVessel(v, "Vessel destruction event");
        }
        private void Terminated(ProtoVessel pv)
        {
            if (!Active || pv == null)
                return;
            var ctx = pv.vesselRef != null ? FromVessel(pv.vesselRef, null, "Termination event") : new Context { Vessel = pv.vesselName, VesselId = pv.vesselID.ToString(), Situation = pv.situation.ToString(), UT = Now, Quality = "Termination event" };
            var names = new HashSet<string>(pv.GetVesselCrew().Where(c => c != null).Select(c => c.name));
            foreach (var p in snapshots)
                if (p.Value.VesselId == ctx.VesselId && Math.Abs(Now - p.Value.UT) <= 5)
                    names.Add(p.Key);
            foreach (var name in names)
                SetHint(name, "Vessel terminated", "Confirmed", "Tracking Station termination", ctx);
        }
        private static bool Fresh(Hint hint)
        {
            return hint != null && Ledger.EvidenceInWindow(hint.UT, Now, Time.realtimeSinceStartup - hint.Time);
        }
        private void SetHint(string name, string cause, string confidence, string source, Context ctx)
        {
            if (!Active || string.IsNullOrEmpty(name))
                return;
            cause = Ledger.EvaCause(cause, ctx != null && ctx.EVA == "True");
            if (!hints.TryGetValue(name, out Hint previous) || !Fresh(previous) || !Ledger.SameContext(previous.Location, ctx))
                hints[name] = new Hint { Cause = cause, Confidence = confidence, Source = source, Time = Time.realtimeSinceStartup, UT = Now, Location = ctx };
            else if (Ledger.PreferCause(cause, confidence, previous.Cause, previous.Confidence))
                hints[name] = new Hint { Cause = cause, Confidence = confidence, Source = Ledger.MergeEvidence(source, previous.Source), Time = Time.realtimeSinceStartup, UT = Now, Location = ctx ?? previous.Location };
            else
                previous.Source = Ledger.MergeEvidence(previous.Source, source);
            var r = Live.Open(name);
            if (r != null && !r.Imported && r.UT >= 0 && Math.Abs(Now - r.UT) <= 5)
            {
                string oldCause = r.Cause, oldConfidence = r.Confidence, oldSource = r.Source;
                Context oldLocation = r.Location;
                ApplyHint(r);
                if (r.Cause != oldCause || r.Confidence != oldConfidence || r.Source != oldSource || r.Location != oldLocation)
                    Changed(r);
            }
        }
        private void ApplyHint(Casualty r)
        {
            if (r.ExternalCause)
                return;
            // Older saves have no ExternalCause flag. Treat a confirmed cause
            // with a non-stock source as authoritative as well.
            if (r.Confidence == "Confirmed" && !Ledger.IsStockEvidence(r.Source))
                return;
            if (hints.TryGetValue(r.Name, out Hint h) && Fresh(h) && Ledger.SameContext(r.Location, h.Location) &&
                r.UT >= 0 && Math.Abs(h.UT - r.UT) <= 5)
            {
                string cause = Ledger.EvaCause(h.Cause, (h.Location != null && h.Location.EVA == "True") || (r.Location != null && r.Location.EVA == "True"));
                if (Ledger.PreferCause(cause, h.Confidence, r.Cause, r.Confidence))
                {
                    r.Cause = cause;
                    r.Confidence = h.Confidence;
                    r.Source = h.Source;
                }
                else if (r.Cause == cause && r.Confidence == h.Confidence)
                    r.Source = Ledger.MergeEvidence(r.Source, h.Source);
                if (h.Location != null && (r.Location == null || r.Location.Quality == "Last known"))
                {
                    // A vessel-level hint must not discard an existing part ID.
                    if (r.Location == null || string.IsNullOrEmpty(r.Location.PartId) || !string.IsNullOrEmpty(h.Location.PartId))
                        r.Location = h.Location.Copy();
                }
            }
            if (r.Fatal && r.Location != null && r.Location.EVA == "True" && r.Location.UT >= 0 && Math.Abs(r.UT - r.Location.UT) <= 5 && (r.Cause == "Unknown" || r.Cause == "Part destroyed" || r.Cause == "Crash"))
            {
                r.Cause = "EVA accident — unknown cause";
                r.Confidence = "Probable";
                r.Source = Ledger.MergeEvidence(r.Source, "Last known vessel was EVA; specific cause unavailable");
            }
        }
        private void Store(string name, Context ctx)
        {
            if (ctx != null && !string.IsNullOrEmpty(name))
                snapshots[name] = ctx;
        }
        private static Context FromVessel(Vessel v, Part p, string quality)
        {
            var c = new Context { UT = Now, Quality = quality };
            if (v != null)
            {
                c.Vessel = v.vesselName;
                c.VesselId = v.id.ToString();
                c.Body = v.mainBody == null ? "" : v.mainBody.bodyName;
                c.Latitude = v.latitude;
                c.Longitude = v.longitude;
                c.Altitude = v.altitude;
                c.Speed = v.srfSpeed;
                c.Gee = v.loaded ? v.geeForce : double.NaN;
                c.MET = v.missionTime;
                c.Situation = v.situation.ToString();
                c.EVA = v.isEVA.ToString();
            }
            if (p != null)
            {
                c.Part = p.partInfo == null ? p.name : p.partInfo.name;
                c.PartTitle = p.partInfo == null ? p.name : p.partInfo.title;
                c.PartId = p.flightID.ToString();
            }
            return c;
        }
        private void CaptureVessel(Vessel v, string quality)
        {
            if (v == null)
                return;
            if (v.loaded)
            {
                foreach (var p in v.parts)
                    if (p != null)
                        foreach (var c in p.protoModuleCrew)
                            if (c != null && !Lost(c.rosterStatus))
                                Store(c.name, FromVessel(v, p, quality));
            }
            else if (v.protoVessel != null)
            {
                foreach (var p in v.protoVessel.protoPartSnapshots)
                    foreach (var c in p.protoModuleCrew)
                        if (c != null && !Lost(c.rosterStatus))
                        {
                            var ctx = FromVessel(v, null, quality);
                            ctx.Part = p.partName;
                            ctx.PartTitle = p.partInfo == null ? p.partName : p.partInfo.title;
                            ctx.PartId = p.flightID.ToString();
                            Store(c.name, ctx);
                        }
            }
        }
        private void CaptureCurrent(string name)
        {
            if (FlightGlobals.Vessels == null)
                return;
            foreach (var v in FlightGlobals.Vessels)
            {
                if (v == null)
                    continue;
                if (v.loaded)
                {
                    foreach (var p in v.parts)
                        if (p != null && p.protoModuleCrew.Any(c => c != null && c.name == name))
                        {
                            Store(name, FromVessel(v, p, "Status event"));
                            return;
                        }
                }
                else if (v.protoVessel != null)
                {
                    foreach (var p in v.protoVessel.protoPartSnapshots)
                        if (p.protoModuleCrew.Any(c => c != null && c.name == name))
                        {
                            var ctx = FromVessel(v, null, "Status event (unloaded)");
                            ctx.Part = p.partName;
                            ctx.PartTitle = p.partInfo == null ? p.partName : p.partInfo.title;
                            ctx.PartId = p.flightID.ToString();
                            Store(name, ctx);
                            return;
                        }
                }
            }
        }
        private Casualty Record(ProtoCrewMember crew, RS status, bool killed, bool imported, string source)
        {
            if (!Active)
                return null;
            double observedAt = Time.realtimeSinceStartup;
            var old = Live.Open(crew.name);
            bool wasFatal = old != null && old.Fatal;
            var r = Live.Observe(crew.name, status.ToString(), Now, killed, imported);
            if (r == null)
                return null;
            if (!r.Closed)
            {
                if (string.IsNullOrEmpty(r.PortraitPng))
                {
                    r.PortraitPng = portraits.Get(crew.name);
                }
                r.Trait = crew.trait;
                r.Gender = crew.gender.ToString();
                r.CrewType = crew.type.ToString();
                r.Level = crew.experienceLevel.ToString();
                if (r.Source.Length == 0)
                    r.Source = source;
                if (r.Location == null && !imported && snapshots.TryGetValue(crew.name, out Context ctx))
                    r.Location = ctx.Copy();
                if (!imported)
                    ApplyHint(r);
                r.History = History(crew);
                if (!imported && Options.notifications && (old == null || (!wasFatal && r.Fatal)))
                    ScreenMessages.PostScreenMessage(crew.name + (r.Fatal ? " has died." : " is missing."), 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                snapshots.Remove(crew.name);
                hints.Remove(crew.name);
            }
            // Assign new losses immediately, and clear temporary messages on return.
            if (Inscriptions.NeedsMessage(r))
            {
                EnsureArchive();
                Casualty previous = null;
                if (Permanent)
                    archive.TryGetValue(r.Id, out previous);
                Inscriptions.AssignFrom(r, previous,
                    () => PickInscription(false, ReadInscriptionPool(false)),
                    () => PickInscription(true, ReadInscriptionPool(true)));
            }
            Changed(r);
            // Only newly confirmed deaths, not old records imported on load.
            DeathPlaques.Observe(r, wasFatal, imported, observedAt, Options.showDeathPlaques, Options.deathPlaqueDelay);
            return r;
        }
        private static List<string> ReadInscriptionPool(bool missing)
        {
            return Inscriptions.ReadStatusPool(System.IO.Path.Combine(KSPUtil.ApplicationRootPath,
                "GameData", "KerbalMemorial", "PluginData", "Inscriptions"), missing,
                message => Log.Warning("[KerbalMemorial] " + message));
        }
        private string PickInscription(bool missing, List<string> pool)
        {
            // Older saves have no cycle data. Seed once from their actual saved
            // inscriptions, then persist the cycle independently of record status.
            inscriptionCycles.Seed(Live.Records);
            string message = inscriptionCycles.For(missing).Pick(pool);
            if (Permanent && archiveLoaded && !string.IsNullOrEmpty(message))
            {
                archiveCycles.CopyFrom(inscriptionCycles);
                archiveDirty = true;
            }
            return message;
        }
        private void RefreshInscriptions(bool force = false)
        {
            if (!Active || (!force && Time.realtimeSinceStartup < nextInscriptionPoll))
                return;
            nextInscriptionPoll = Time.realtimeSinceStartup + 10f;
            BackfillInscriptions();
        }
        private void BackfillInscriptions()
        {
            EnsureArchive();
            // Read each pool at most once per pass, and only if needed.
            List<string> deceased = null, missing = null;
            Func<string> pick = () => PickInscription(false, deceased ?? (deceased = ReadInscriptionPool(false)));
            Func<string> pickMissing = () => PickInscription(true, missing ?? (missing = ReadInscriptionPool(true)));
            bool includeArchive = Permanent && archiveLoaded;
            if (Inscriptions.Backfill(Live.Records, includeArchive ? archive : null, pick, pickMissing))
                archiveDirty = true;
            if (includeArchive && archiveDirty)
                FlushArchive();
        }
        private static string History(ProtoCrewMember crew)
        {
            var node = new ConfigNode("CAREER_LOG");
            if (crew.careerLog != null)
                crew.careerLog.Save(node);
            var flight = node.AddNode("FLIGHT_LOG");
            if (crew.flightLog != null)
                crew.flightLog.Save(flight);
            return node.ToString();
        }
        internal bool Report(ProtoCrewMember crew, string cause, string source)
        {
            if (!Active || crew == null || string.IsNullOrWhiteSpace(cause))
                return false;
            CaptureCurrent(crew.name);
            var r = Record(crew, Lost(crew.rosterStatus) ? crew.rosterStatus : RS.Missing, true, false, source);
            if (r == null)
                return false;
            r.Cause = cause;
            r.Confidence = "Confirmed";
            r.Source = source;
            r.ExternalCause = true;
            Changed(r);
            return true;
        }
        private void EnsureArchive()
        {
            if (KrashIntegration.IsSimulationActive)
                return;
            if (!Permanent)
            {
                wasPermanent = false;
                return;
            }
            if (!archiveLoaded)
            {
                try
                {
                    foreach (var r in Persistence.LoadArchive(out archiveCycles))
                        archive[r.Id] = r;
                    archiveLoaded = true;
                    Error = null;
                }
                catch (Exception e)
                {
                    Error = "Cannot read casualty archive: " + e.Message;
                    return;
                }
            }
            if (!wasPermanent)
            {
                // The archive owns the cycle across reverts. On the first
                // activation seed legacy history, and include live-only records.
                // On subsequent toggles in this scenario the live cycle has
                // continued from the last archived cycle, so keep that progress.
                if (archiveCycles.Deceased.Initialized || archiveCycles.Missing.Initialized)
                {
                    if (!archiveCycleAdopted)
                    {
                        var liveOnly = Live.Records.Where(r => !archive.ContainsKey(r.Id)).ToList();
                        archiveCycles.Seed(Live.Records.Concat(archive.Values));
                        archiveCycles.Deceased.Used.UnionWith(liveOnly.Select(Inscriptions.FatalMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
                        archiveCycles.Missing.Used.UnionWith(liveOnly.Select(Inscriptions.SavedMissingMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
                        inscriptionCycles.CopyFrom(archiveCycles);
                    }
                }
                else
                {
                    // Keep a live cycle if an archive is created for the first time;
                    // for a legacy archive include its already assigned messages.
                    inscriptionCycles.Seed(Live.Records.Concat(archive.Values));
                    inscriptionCycles.Deceased.Used.UnionWith(archive.Values.Where(r => !Live.Records.Any(l => l.Id == r.Id)).Select(Inscriptions.FatalMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
                    inscriptionCycles.Missing.Used.UnionWith(archive.Values.Where(r => !Live.Records.Any(l => l.Id == r.Id)).Select(Inscriptions.SavedMissingMessage).Where(m => !string.IsNullOrWhiteSpace(m)));
                }
                archiveCycleAdopted = true;
                archiveCycles.CopyFrom(inscriptionCycles);
                archiveDirty = true;
                wasPermanent = true;
                // The newly visible archive also needs its inscription check.
                nextInscriptionPoll = 0;
                foreach (var r in Live.Records)
                    if (!archive.ContainsKey(r.Id))
                    {
                        archive[r.Id] = r.Copy();
                        archiveDirty = true;
                    }
            }
        }
        private void Changed(Casualty r)
        {
            EnsureArchive();
            if (!Permanent || !archiveLoaded)
                return;
            var copy = r.Copy();
            if (archive.TryGetValue(r.Id, out Casualty previous))
            {
                if (previous.Closed && !copy.Closed)
                    copy.ReturnUT = previous.ReturnUT;
                if (string.IsNullOrEmpty(copy.PortraitPng))
                    copy.PortraitPng = previous.PortraitPng;
                // Reuse only a compatible saved inscription, including random missing messages.
                Inscriptions.AssignFrom(copy, previous, null);
                if (previous.ExternalCause && !copy.ExternalCause)
                {
                    copy.Cause = previous.Cause;
                    copy.Confidence = previous.Confidence;
                    copy.Source = previous.Source;
                    copy.ExternalCause = true;
                }
            }
            archive[r.Id] = copy;
            archiveDirty = true;
            FlushArchive(); // persist immediately before a user can revert or quickload
        }
        private void FlushArchive()
        {
            if (!archiveLoaded || !archiveDirty || saveFolder != HighLogic.SaveFolder || KrashIntegration.IsSimulationActive)
                return;
            try
            {
                Persistence.SaveArchive(archive.Values, archiveCycles);
                archiveDirty = false;
                Error = null;
            }
            catch (Exception e)
            {
                Error = "Cannot save casualty archive: " + e.Message;
                Log.Warning("[KerbalMemorial] " + Error);
            }
            nextWrite = Time.realtimeSinceStartup + 5f;
        }
        internal List<Casualty> VisibleRecords(bool refreshInscriptions = false)
        {
            EnsureArchive();
            // On the first load of the wall's list, backfill before returning
            // records to any GUI. Regular Layout/Repaint refreshes retain the
            // retry interval, avoiding disk reads each frame if the pool is empty.
            RefreshInscriptions(refreshInscriptions);
            var result = Live.Records.ToList();
            if (Permanent)
            {
                var ids = new HashSet<string>(result.Select(r => r.Id));
                result.AddRange(archive.Values.Where(r => !ids.Contains(r.Id)));
            }
            return result.OrderByDescending(r => r.UT).ThenBy(r => r.Name).ToList();
        }
        internal bool IsLive(Casualty r) => Live.Records.Any(x => x.Id == r.Id);
        internal void EditNote(Casualty r, string note)
        {
            if (KrashIntegration.IsSimulationActive || r.Notes == note)
                return;
            r.Notes = note;
            if (IsLive(r))
                Changed(r);
            else if (Permanent)
            {
                archiveDirty = true;
                FlushArchive();
            }
        }
    }
    public static class MemorialAPI
    {
        public const int Version = 1;
        // Report only actual fatalities. This does not kill or otherwise alter the crew member.
        public static bool ReportDeath(ProtoCrewMember crew, string cause, string sourceMod)
        {
            return CasualtyTracker.Instance != null && CasualtyTracker.Instance.Report(crew, cause, sourceMod ?? "External mod");
        }
    }
}
