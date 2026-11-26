using System;
using System.Linq;
using System.IO;
using KerbalMemorial;
internal static class LedgerTests
{
    private static int count;
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        count++;
    }
    public static void Main()
    {
        var l = new Ledger();
        var a = l.Observe("Jeb", "Missing", 10);
        Check(!a.Fatal, "Missing is not death");
        Check(l.Observe("Jeb", "Missing", 10, true) == a && a.Fatal, "Death report upgrades MIA");
        Check(l.Observe("Jeb", "Dead", 11) == a && l.Records.Count == 1, "Duplicate events merge");
        Check(a.DeathUT == 10, "First confirmation time retained");
        l.Observe("Jeb", "Available", 20);
        Check(a.Closed && a.DisplayStatus == "Respawned", "Respawn preserves death");
        var b = l.Observe("Jeb", "Dead", 30);
        Check(b.Id != a.Id && l.Records.Count == 2, "Second death separate");
        l.Rename("Jeb", "Jebediah");
        Check(l.Open("Jebediah") == b && a.OriginalName == "Jeb", "Rename keeps original");
        var c = l.Observe("Bob", "Missing", 40);
        l.Observe("Bob", "Assigned", 50);
        Check(c.DisplayStatus == "Returned" && !c.Fatal, "Ordinary missing return");
        var d = l.Observe("Bill", "Dead", 60, false, true);
        Check(d.Fatal && d.UT == -1 && d.DeathUT == -1, "Imported death unknown date");
        Ledger.Cause(d, "Crash", "Probable", "event");
        Ledger.Cause(d, "Impact", "Confirmed", "event");
        Ledger.Cause(d, "Unknown", "Unknown", "event");
        Check(d.Cause == "Impact", "Cause confidence priority");
        d.Location = new Context { Vessel = "Test" };
        var copy = d.Copy();
        copy.Location.Vessel = "Other";
        Check(d.Location.Vessel == "Test", "Archive snapshot isolated");
        d.PortraitPng = "portrait";
        Check(d.Copy().PortraitPng == "portrait", "Archive copy preserves portrait data");
        Check(Ledger.IsMemorialLoss(a), "Respawned fatality included on wall");
        Check(Ledger.IsMemorialLoss(b), "Current fatality included on wall");
        Check(!Ledger.IsMemorialLoss(c), "Returned ordinary MIA excluded from wall");
        Check(Ledger.IsMemorialLoss(d), "Imported death included on wall");
        Check(Ledger.IsMemorialLoss(new Casualty { Status = "Missing" }), "Missing crew included on wall");
        Check(!Ledger.IsMemorialLoss(null), "Null record excluded from wall");
        Check(Ledger.Csv("a,\"b\"") == "\"a,\"\"b\"\"\"", "CSV quoting");
        Check(l.Observe("Nobody", "Available", 5) == null, "No casualty on ordinary status");
        Check(Ledger.LatestIncidentUT(l.Records, 70) == 40, "Returned missing incident remains latest; unknown import ignored");
        Check(Ledger.LatestIncidentUT(l.Records, 35) == 30, "Future timeline incident excluded");
        Check(Ledger.LatestIncidentUT(l.Records, 9) == -1, "No dated incident yet");
        Check(Ledger.LatestIncidentUT(new Casualty[] { d }, 100) == -1, "Unknown incident date excluded");
        Check(Ledger.LatestIncidentUT(new Casualty[] { new Casualty { UT = 0 } }, 0) == 0, "Zero UT is valid");
        Check(Ledger.LatestIncidentUT(l.Records, double.NaN) == -1, "Invalid clock ignored");
        Check(Ledger.FormatElapsedDays(0, 21600) == "0 days", "Immediately after incident");
        Check(Ledger.FormatElapsedDays(21599, 21600) == "0 days", "Partial day rounded down");
        Check(Ledger.FormatElapsedDays(21600, 21600) == "1 day", "Exactly one Kerbin day");
        Check(Ledger.FormatElapsedDays(64799, 21600) == "2 days", "Whole days only");
        Check(Ledger.FormatElapsedDays(86400, 86400) == "1 day", "Earth calendar day");
        Check(Ledger.FormatElapsedDays(432000, 43200) == "10 days", "Custom calendar day");
        Check(Ledger.FormatElapsedDays(21600 * 500d, 21600) == "500 days", "No year conversion");
        Check(Ledger.FormatElapsedDays(double.NaN, 21600) == "Unknown", "Invalid elapsed time");
        Check(Ledger.FormatElapsedDays(10, 0) == "Unknown", "Invalid calendar day");
        Check(Ledger.SafetySignDays(new Casualty[0], 0, 21600) == "0 DAYS", "New game sign");
        Check(Ledger.SafetySignDays(new Casualty[0], 43201, 21600) == "2 DAYS", "No incidents: count from UT zero");
        Check(Ledger.SafetySignDays(l.Records, 40, 21600) == "0 DAYS", "Sign resets at latest incident");
        Check(Ledger.SafetySignDays(l.Records, 21640, 21600) == "1 DAYS", "Sign fixed uppercase wording");
        Check(Ledger.SafetySignDays(l.Records, 35, 21600) == "0 DAYS", "Sign excludes later timeline");
        Check(Ledger.SafetySignDays(new Casualty[] { d }, 100, 21600) == "--- DAYS", "Undated history sign");
        Check(Ledger.SafetySignDays(new Casualty[0], 86400, 86400) == "1 DAYS", "Sign uses active calendar");
        Check(Ledger.SafetySignDays(new Casualty[0], double.NaN, 21600) == "--- DAYS", "Sign invalid clock");
        Check(Ledger.SafetySignDays(new Casualty[0], 10, 0) == "--- DAYS", "Sign invalid calendar");
        var flat = new int[64 * 64];
        var noise = new int[flat.Length];
        var stripes = new int[flat.Length];
        var face = new int[flat.Length];
        var random = new Random(123);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int i = y * 64 + x;
                flat[i] = 300;
                noise[i] = random.Next(766);
                stripes[i] = y % 2 == 0 ? 100 : 650;
                face[i] = (x - 32) * (x - 32) + (y - 32) * (y - 32) < 400 ? 400 : 100;
            }
        Check(!PortraitQuality.IsUsable(flat, 64, 64), "Blank portrait rejected");
        Check(!PortraitQuality.IsUsable(noise, 64, 64), "Random static rejected");
        Check(!PortraitQuality.IsUsable(stripes, 64, 64), "Alternating scanlines rejected");
        Check(PortraitQuality.IsUsable(face, 64, 64), "Coherent portrait accepted");
        Check(!PortraitQuality.IsUsable(face, 1, 64), "Bad dimensions rejected");
        var parsed = Inscriptions.Parse(new[] { "### **Short plaque inscriptions**", "", "- **Reach for the stars.**", "2. Never forgotten.", "* Still exploring.", "---", "### Another heading", "A plain line." }).ToArray();
        Check(parsed.SequenceEqual(new[] { "Reach for the stars.", "Never forgotten.", "Still exploring.", "A plain line." }), "Inscription parser skips headings and list formatting");
        var loss = new Casualty { Status = "Missing", Notes = "Old note" };
        int picks = 0;
        Func<string> choose = () =>
        {
            picks++;
            return "Among the stars.";
        };
        Check(Inscriptions.Assign(loss, choose) && picks == 0 && loss.Inscription == Inscriptions.MissingMessage && loss.InscriptionForMissing, "Missing crew get fixed inscription without a random pick");
        Check(!Inscriptions.Assign(loss, choose) && picks == 0, "Missing inscription remains stable");
        loss.ReportedKilled = true;
        Check(Inscriptions.Assign(loss, choose) && picks == 1, "Assign at first fatality confirmation");
        Check(!Inscriptions.Assign(loss, choose) && picks == 1, "Duplicate death does not reselect");
        Check(!Inscriptions.Assign(loss, choose) && picks == 1, "Existing inscription never replaced");
        Check(Inscriptions.Display(loss) == "Among the stars." && loss.Notes == "Old note", "Inscription independent of notes");
        Check(Inscriptions.Display(new Casualty { Status = "Dead", Notes = "Legacy text" }) == "Legacy text", "Legacy note remains visible");
        Check(loss.Copy().Inscription == loss.Inscription, "Archive copy retains inscription");
        var imported = new Casualty { Status = "Dead", Imported = true, UT = -1 };
        Check(Inscriptions.Assign(imported, choose), "Historical imports receive inscriptions");
        Check(!Inscriptions.Assign(imported, choose), "Backfill is idempotent");
        var legacy = new Casualty { Status = "Dead", ReturnUT = 100, Inscription = " " };
        Check(Inscriptions.Assign(legacy, choose), "Previously respawned fatality receives inscription");
        var current = new Casualty { Status = "Dead" };
        var archived = new Casualty { Status = "Dead", Inscription = "Existing archive message" };
        Check(!Inscriptions.FillPair(current, archived, () =>
            {
            throw new Exception("Unexpected pick");
            }) && current.Inscription == archived.Inscription, "Restore archived inscription without reselection");
        archived.Inscription = "";
        Check(Inscriptions.FillPair(current, archived, () =>
            {
            throw new Exception("Unexpected pick");
            }) && archived.Inscription == current.Inscription, "Fill archive from current record");
        current.Inscription = archived.Inscription = "";
        int batchPicks = 0;
        Check(Inscriptions.FillPair(current, archived, () =>
            {
            batchPicks++;
            return "Shared message";
            }) && batchPicks == 1 && current.Inscription == archived.Inscription, "One selection for both copies");
        var retry = new Casualty { Status = "Dead" };
        Check(!Inscriptions.Assign(retry, () => "") && Inscriptions.Assign(retry, choose), "Retry after empty pool");
        Check(!Inscriptions.Assign(new Casualty { Status = "Dead" }, () => ""), "Empty pool does not assign");
        var loadedDead = new Casualty { Status = "Dead", Imported = true };
        var loadedRespawned = new Casualty { Status = "Dead", ReturnUT = 100, Inscription = "  " };
        var loadedMissing = new Casualty { Status = "Missing" };
        var loadedExisting = new Casualty { Status = "Dead", Inscription = "Keep this" };
        var loadedReported = new Casualty { Status = "Missing", ReportedKilled = true };
        var oldArchive = loadedDead.Copy();
        oldArchive.Inscription = "Saved archive message";
        var archiveOnly = new Casualty { Status = "Dead" };
        var pairedRespawned = loadedRespawned.Copy();
        var loaded = new[] { loadedDead, loadedRespawned, loadedMissing, loadedExisting, loadedReported };
        var loadedArchive = new System.Collections.Generic.Dictionary<string, Casualty>
        { { oldArchive.Id, oldArchive }, { archiveOnly.Id, archiveOnly }, { pairedRespawned.Id, pairedRespawned } };
        int loadPicks = 0;
        Func<string> loadPick = () =>
        {
            loadPicks++;
            return "Load message " + loadPicks;
        };
        Check(Inscriptions.Backfill(loaded, loadedArchive, loadPick), "Loaded list marks changed archive for saving");
        Check(loadedDead.Inscription == oldArchive.Inscription && loadedDead.Inscription == "Saved archive message", "Loaded death recovers saved archive inscription");
        Check(loadedRespawned.Inscription == pairedRespawned.Inscription && !string.IsNullOrWhiteSpace(loadedRespawned.Inscription), "Loaded respawned death receives shared inscription");
        Check(loadedMissing.Inscription == Inscriptions.MissingMessage && loadedExisting.Inscription == "Keep this", "Load assigns missing message and preserves fatality inscriptions");
        Check(!string.IsNullOrWhiteSpace(loadedReported.Inscription) && !string.IsNullOrWhiteSpace(archiveOnly.Inscription) && loadPicks == 3, "Load assigns reported and archived fatalities exactly once");
        Check(!Inscriptions.Backfill(loaded, loadedArchive, loadPick) && loadPicks == 3, "Repeated list loads never reroll inscriptions");
        var saveOnly = new Casualty { Status = "Dead" };
        Check(!Inscriptions.Backfill(new[] { saveOnly }, null, () => "") && saveOnly.Inscription == "", "Empty pool is safe during list load");
        Check(!Inscriptions.Backfill(new[] { saveOnly }, null, loadPick) && saveOnly.Inscription == "Load message 4", "Save-only list backfill updates the actual record");
        var returned = new Casualty { Status = "Missing", Notes = "Return note" };
        Check(Inscriptions.Assign(returned, null) && Inscriptions.Display(returned) == "Missing, but not forgotten", "Fixed message requires no pool");
        returned.ReturnUT = 42;
        Check(Inscriptions.Assign(returned, null) && returned.Inscription == "" && !returned.InscriptionForMissing && Inscriptions.Display(returned) == "Return note", "Returned crew lose temporary inscription");
        var upgrading = new Casualty { Status = "Missing" };
        Inscriptions.Assign(upgrading, null);
        var missingArchive = upgrading.Copy();
        upgrading.Status = "Dead";
        Check(!Inscriptions.FillPair(upgrading, missingArchive, () => "") && upgrading.Inscription == "" && !upgrading.InscriptionForMissing, "Fatality never inherits archive missing message even with empty pool");
        Check(Inscriptions.Assign(upgrading, () => "Final tribute") && upgrading.Inscription == "Final tribute", "Fatality retries normal assignment");
        missingArchive.Status = "Dead";
        Check(Inscriptions.FillPair(upgrading, missingArchive, null) && missingArchive.Inscription == "Final tribute" && !missingArchive.InscriptionForMissing, "Archive death upgrades to saved fatality inscription");
        var archiveMissingOnly = new Casualty { Status = "Missing" };
        var missingOnlyArchive = new System.Collections.Generic.Dictionary<string, Casualty> { { archiveMissingOnly.Id, archiveMissingOnly } };
        Check(Inscriptions.Backfill(new Casualty[0], missingOnlyArchive, null) && archiveMissingOnly.Inscription == Inscriptions.MissingMessage, "Archive-only missing crew backfill without pool");
        var literal = new Casualty { Status = "Dead" };
        Check(Inscriptions.Assign(literal, () => Inscriptions.MissingMessage) && !literal.InscriptionForMissing && !Inscriptions.Assign(literal, choose), "A matching literal fatality inscription is not a temporary message");
        int missingPicks = 0;
        Func<string> selectMissing = () =>
        {
            missingPicks++;
            return "Still searching.";
        };
        Func<string> neverPick = () =>
        {
            throw new Exception("Wrong inscription pool or unexpected reselection");
        };
        var randomMissing = new Casualty { Status = "Missing" };
        Check(Inscriptions.Assign(randomMissing, neverPick, selectMissing) && randomMissing.InscriptionForMissing && randomMissing.InscriptionFromMissingPool && Inscriptions.Display(randomMissing) == "Still searching.", "Missing uses its own pool and displays saved message");
        Check(!Inscriptions.Assign(randomMissing, neverPick, neverPick) && missingPicks == 1, "Saved missing inscription is stable");
        var oldFixed = new Casualty { Status = "Missing", Inscription = Inscriptions.MissingMessage, InscriptionForMissing = true };
        Check(Inscriptions.NeedsMessage(oldFixed) && !Inscriptions.Assign(oldFixed, neverPick, () => ""), "Old fallback waits for missing pool without changing record");
        Check(Inscriptions.Assign(oldFixed, neverPick, selectMissing) && oldFixed.InscriptionFromMissingPool && oldFixed.Inscription == "Still searching.", "Legacy fixed missing inscription is upgraded");
        var sameText = new Casualty { Status = "Missing" };
        Check(Inscriptions.Assign(sameText, neverPick, () => Inscriptions.MissingMessage) && !Inscriptions.NeedsMessage(sameText), "Random selection matching fallback text does not reroll");
        var missingLive = new Casualty { Status = "Missing" };
        var missingCopy = missingLive.Copy();
        int pairedPicks = 0;
        Check(Inscriptions.FillPair(missingLive, missingCopy, neverPick, () =>
            {
            pairedPicks++;
            return "Awaiting your return.";
            }) && pairedPicks == 1 && missingLive.Inscription == missingCopy.Inscription && missingCopy.InscriptionFromMissingPool, "Live and archive missing records share one selection");
        var missingReload = new Casualty { Status = "Missing", Id = missingCopy.Id };
        Check(!Inscriptions.FillPair(missingReload, missingCopy, neverPick, neverPick) && missingReload.Inscription == missingCopy.Inscription && missingReload.InscriptionFromMissingPool, "Restore random missing message from archive");
        var mixedDeath = new Casualty { Status = "Dead", Id = missingLive.Id };
        Check(!Inscriptions.FillPair(mixedDeath, missingCopy, () => "Final tribute", neverPick) && mixedDeath.Inscription == "Final tribute" && missingCopy.Inscription == "Awaiting your return.", "Different timeline statuses keep separate inscriptions");
        randomMissing.ReportedKilled = true;
        Check(Inscriptions.Assign(randomMissing, () => "", neverPick) && randomMissing.Inscription == "" && !randomMissing.InscriptionFromMissingPool, "Fatality clears random missing inscription even if deceased pool empty");
        Check(Inscriptions.Assign(randomMissing, () => "Final tribute", neverPick) && randomMissing.Inscription == "Final tribute" && !randomMissing.InscriptionForMissing, "Confirmed death selects only deceased pool");
        missingLive.ReturnUT = 42;
        Check(Inscriptions.Assign(missingLive, neverPick, neverPick) && missingLive.Inscription == "" && !missingLive.InscriptionForMissing && !missingLive.InscriptionFromMissingPool, "Return clears random missing inscription");
        var onlyMissing = new Casualty { Status = "Missing" };
        var onlyMissingArchive = new System.Collections.Generic.Dictionary<string, Casualty> { { onlyMissing.Id, onlyMissing } };
        Check(Inscriptions.Backfill(new Casualty[0], onlyMissingArchive, neverPick, selectMissing) && onlyMissing.Inscription == "Still searching.", "Archive-only missing record uses missing pool");
        string directory = Path.Combine(Path.GetTempPath(), "KerbalMemorial-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(Inscriptions.Pick(directory) == "", "Missing directory is safe");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "a.txt"), new[] { "### **Short plaque inscriptions**", "- First message" });
            File.WriteAllLines(Path.Combine(directory, "b.TXT"), new[] { "### **Other inscriptions**", "1. Second message" });
            File.WriteAllText(Path.Combine(directory, "README.md"), "Not an inscription");
            Check(Enumerable.Range(0, 20).All(i => new[] { "First message", "Second message" }.Contains(Inscriptions.Pick(directory))), "Only valid text file messages selected");
            Check(Inscriptions.ReadStatusPool(directory, false).Count == 0 && Inscriptions.ReadStatusPool(directory, true).Count == 0, "Old parent files never enter either status pool");
            Directory.CreateDirectory(Path.Combine(directory, "Deceased"));
            Directory.CreateDirectory(Path.Combine(directory, "Missing"));
            File.WriteAllLines(Path.Combine(directory, "Deceased", "death.txt"), new[] { "### **Deceased**", "- Final tribute" });
            File.WriteAllLines(Path.Combine(directory, "Missing", "mia.TXT"), new[] { "### **Missing**", "- Still searching.", "1. Awaiting your return." });
            Check(Inscriptions.ReadStatusPool(directory, false).SequenceEqual(new[] { "Final tribute" }), "Deceased pool contains only deceased messages");
            Check(Inscriptions.ReadStatusPool(directory, true).OrderBy(x => x).SequenceEqual(new[] { "Awaiting your return.", "Still searching." }), "Missing pool contains only missing messages");
            Check(Inscriptions.Assign(new Casualty { Status = "Missing" }, neverPick, () => Inscriptions.Pick(Inscriptions.ReadStatusPool(directory, true))), "Assignment works with actual missing files");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        var spaced = new Casualty { Status = "Dead", DeathUT = 22000, Location = new Context { EVA = "True", Situation = "ORBITING", UT = 22000, EvaDepartureUT = 400 } };
        Check(Ledger.ApplySpaced(spaced, 21600) && spaced.Cause == "Spaced" && spaced.Confidence == "Probable", "Orbital EVA death at exactly one day");
        Check(spaced.Source.Contains("21600.0 s"), "Evidence includes elapsed time since hatch");
        Check(!Ledger.ApplySpaced(spaced, 21600), "Duplicate reports do not reclassify Spaced");
        var evaCase = spaced.Copy();
        evaCase.Cause = "Unknown";
        evaCase.Source = "";
        evaCase.Location.EvaDepartureUT = 399;
        Check(Ledger.ApplySpaced(evaCase, 21600) && evaCase.Confidence == "Possible", "Older EVA still eligible at lower confidence");
        evaCase = spaced.Copy();
        evaCase.Cause = "Part destroyed";
        evaCase.Source = "onPartDie";
        evaCase.Location.EvaDepartureUT = -1;
        Check(Ledger.ApplySpaced(evaCase, 21600) && evaCase.Confidence == "Possible", "Generic destruction with unknown EVA departure");
        evaCase = spaced.Copy();
        evaCase.Cause = "Unknown";
        evaCase.Location.Situation = "ESCAPING";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Escaping is not orbiting");
        evaCase.Location.Situation = "SUB_ORBITAL";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Suborbital EVA not Spaced");
        evaCase.Location.Situation = "LANDED";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Surface EVA not Spaced");
        evaCase.Location.Situation = "ORBITING";
        evaCase.Location.EVA = "False";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Crew inside vessel not Spaced");
        evaCase.Location.EVA = "True";
        evaCase.Status = "Missing";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Missing alone is not Spaced");
        evaCase.Status = "Dead";
        evaCase.Imported = true;
        Check(!Ledger.ApplySpaced(evaCase, 21600), "No reclassification of imported casualties");
        evaCase.Imported = false;
        evaCase.ExternalCause = true;
        Check(!Ledger.ApplySpaced(evaCase, 21600), "External cause protected");
        evaCase.ExternalCause = false;
        evaCase.ReturnUT = 22001;
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Closed casualty protected");
        evaCase.ReturnUT = -1;
        evaCase.Location.UT = 21994;
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Stale context rejected");
        evaCase.Location.ObservedRealtime = 100;
        Check(Ledger.ApplySpaced(evaCase, 21600, 101) && evaCase.Confidence == "Possible", "Recent real-time snapshot under warp is lower confidence");
        evaCase.Cause = "Unknown";
        Check(!Ledger.ApplySpaced(evaCase, 21600, 103), "Stale real-time snapshot rejected");
        evaCase.Location.UT = 22000;
        evaCase.Source = "onPartDie | onCrash";
        Check(!Ledger.ApplySpaced(evaCase, 21600), "Merged impact evidence blocks Spaced");
        evaCase.Source = "";
        foreach (string specific in new[] { "Cooked", "Atomized", "Stomped (1)", "Stomped (2)", Ledger.BuriedCause, Ledger.DrownedCause, "Crash", "EVA collision", "Structural failure", "Left for dead", "Life support failure" })
        {
            evaCase.Cause = specific;
            Check(!Ledger.ApplySpaced(evaCase, 21600), "Known cause blocks Spaced: " + specific);
            Check(Ledger.PreferEvidence(specific, "Possible", "event", "Spaced", "Probable", "Orbital EVA death"), "Late specific cause supersedes Spaced: " + specific);
        }
        Check(!Ledger.PreferEvidence("Part destroyed", "Confirmed", "onPartDie", "Spaced", "Possible", "Orbital EVA death"), "Generic destruction cannot erase Spaced");
        Check(!Ledger.PreferEvidence("Spaced", "Probable", "event", "Cooked", "Confirmed", "onOverheat"), "Spaced never displaces specific cause");
        const string drownedSource = "onPartDie; part destroyed while vessel situation was SPLASHED";
        foreach (string otherCause in new[] { "Part destroyed", "Water impact", "EVA water impact", "Ground impact", "Cooked", "Atomized", "Stomped (1)", "Stomped (2)", Ledger.SpacedCause })
        {
            Check(Ledger.PreferEvidence(Ledger.DrownedCause, "Confirmed", drownedSource, otherCause, "Confirmed", "onCrash"), "Splashed destruction overrides " + otherCause);
            Check(!Ledger.PreferEvidence(otherCause, "Confirmed", "onCrash", Ledger.DrownedCause, "Confirmed", drownedSource), "Later stock report preserves Drowned: " + otherCause);
        }
        Check(Ledger.PreferEvidence(Ledger.BuriedCause, "Confirmed", "onPartDie", Ledger.DrownedCause, "Confirmed", drownedSource), "Burial overrides Drowned");
        Check(!Ledger.PreferEvidence(Ledger.DrownedCause, "Confirmed", drownedSource, Ledger.BuriedCause, "Confirmed", "onPartDie"), "Drowned cannot replace burial");
        Check(!Ledger.PreferEvidence(Ledger.DrownedCause, "Confirmed", drownedSource, "Life support failure", "Confirmed", "OtherMod"), "External cause protected from Drowned");
        Check(Ledger.EvaCause(Ledger.DrownedCause, true) == Ledger.DrownedCause, "Drowned label applies to EVA");
        Check(Ledger.IsStockEvidence(drownedSource), "Drowned evidence recognized as stock");
        Check(!Ledger.IsImpactEvidence(Ledger.DrownedCause, drownedSource), "Splashed destruction alone is not an impact report");
        Check(Ledger.IsBuried(99.5, 100), "Buried exactly half a meter");
        Check(Ledger.IsBuried(90, 100), "Buried below elevated terrain");
        Check(!Ledger.IsBuried(99.51, 100), "Small contact penetration ignored");
        Check(!Ledger.IsBuried(100, 100), "Surface contact not buried");
        Check(!Ledger.IsBuried(101, 100), "Above terrain not buried");
        Check(!Ledger.IsBuried(-10, -100), "Underwater above seabed not buried");
        Check(Ledger.IsBuried(-101, -100), "Below seabed buried");
        Check(!Ledger.IsBuried(double.NaN, 100), "Invalid part altitude ignored");
        Check(!Ledger.IsBuried(0, double.NaN), "Unknown terrain ignored");
        Check(!Ledger.IsBuried(double.NegativeInfinity, 100), "Infinite altitude ignored");
        Check(!Ledger.IsBuried(0, double.PositiveInfinity), "Infinite terrain ignored");
        Check(!Ledger.IsBuried(-double.MaxValue, double.MaxValue), "Overflow depth ignored");
        Check(Ledger.PreferEvidence(Ledger.BuriedCause, "Confirmed", "onPartDie", "Ground impact", "Confirmed", "onPartExplodeGroundCollision"), "Buried overrides ground impact");
        Check(Ledger.PreferEvidence(Ledger.BuriedCause, "Confirmed", "onPartDie", "Stomped (1)", "Probable", "onCrash"), "Buried overrides severe impact");
        Check(Ledger.PreferEvidence(Ledger.BuriedCause, "Confirmed", "onPartDie", "Stomped (2)", "Confirmed", "onOverG"), "Buried overrides high gee");
        Check(!Ledger.PreferEvidence("Stomped (1)", "Probable", "onCrash", Ledger.BuriedCause, "Confirmed", "onPartDie"), "Late impact cannot replace burial");
        Check(!Ledger.PreferEvidence("Atomized", "Confirmed", "onOverheat", Ledger.BuriedCause, "Confirmed", "onPartDie"), "Late heat cannot replace burial");
        Check(!Ledger.PreferEvidence(Ledger.BuriedCause, "Confirmed", "onPartDie", "Life support failure", "Confirmed", "OtherMod"), "External cause stays authoritative");
        Check(Ledger.EvaCause(Ledger.BuriedCause, true) == Ledger.BuriedCause, "Burial label applies to EVA");
        Check(Ledger.ThermalCause(2200, 2200, 2000, 300, 1000) == "Cooked", "Ordinary overheat cooked");
        Check(Ledger.ThermalCause(9999, 9999, 2000, 300, 1000) == "Cooked", "Four digits cooked");
        Check(Ledger.ThermalCause(10000, 300, 2000, 300, 1000) == "Atomized", "Five digit reported heat atomized");
        Check(Ledger.ThermalCause(double.NaN, 11000, 2000, 300, 1000) == "Atomized", "Extreme internal heat atomized");
        Check(Ledger.ThermalCause(double.NaN, 300, 2000, 20000, 1000) == "Atomized", "Extreme skin heat atomized");
        Check(Ledger.ThermalCause(double.NaN, double.PositiveInfinity, 2000, double.NaN, 1000) == "Cooked", "Invalid telemetry not atomized");
        Check(Ledger.ReportMeasurement("Part 12345 overheated at 12,345.6 K", true) == 12345.6, "Parse grouped reported temperature");
        Check(Ledger.ReportMeasurement("Temperature 1.2e5 K", true) == 120000, "Scientific notation temperature");
        Check(double.IsNaN(Ledger.ReportMeasurement("Part 12345 stage 2", true)), "Ignore unrelated numbers");
        Check(Ledger.EventMeasurement("Impact at 20 m/s", 99, false) == 20, "Explicit speed preferred to param");
        Check(Ledger.EventMeasurement("", 21, false) == 21, "Stock report param fallback");
        Check(double.IsNaN(Ledger.EventMeasurement("", 0, false)), "Absent speed ignored");
        Check(Ledger.ImpactCause("Crash", 20, 10) == "Stomped (1)", "Exactly double crash tolerance");
        Check(Ledger.ImpactCause("Crash", 19.99, 10) == "Crash", "Below double crash tolerance");
        Check(Ledger.ImpactCause("Crash", 20, 0) == "Crash", "Invalid crash tolerance");
        Check(Ledger.ImpactCause("Crash", double.PositiveInfinity, 10) == "Crash", "Invalid impact speed");
        Check(Ledger.DestructionCause("Part destroyed", 15, false) == "Stomped (2)", "Exactly 15 g");
        Check(Ledger.DestructionCause("Part destroyed", 14.99, false) == "Part destroyed", "Below 15 g");
        Check(Ledger.DestructionCause("Part destroyed", 30, true) == "Part destroyed", "Impact is not G-only destruction");
        Check(Ledger.DestructionCause("Part destroyed", double.NaN, false) == "Part destroyed", "Unknown gee ignored");
        Check(Ledger.PreferEvidence("Crash", "Probable", "onCrash", "Stomped (2)", "Confirmed", "onOverG"), "Late crash overrides G-only label");
        Check(!Ledger.PreferEvidence("Stomped (2)", "Confirmed", "onOverG", "Crash", "Probable", "onCrash"), "Early crash prevents G-only label");
        Check(!Ledger.PreferEvidence("Stomped (2)", "Confirmed", "onOverG", "Structural failure", "Probable", "onPartFailure | onCollision: part"), "Merged impact prevents G-only label");
        Check(Ledger.PreferEvidence("Stomped (1)", "Probable", "onCrash", "Ground impact", "Confirmed", "onPartExplodeGroundCollision"), "Specific speed refines ground impact");
        Check(!Ledger.PreferEvidence("Ground impact", "Confirmed", "onPartExplodeGroundCollision", "Stomped (1)", "Probable", "onCrash"), "Generic ground impact cannot erase measured speed");
        Check(Ledger.PreferEvidence("Atomized", "Confirmed", "onOverheat", "Cooked", "Confirmed", "onOverheat"), "Extreme heat refines cooked");
        Check(!Ledger.PreferEvidence("Stomped (2)", "Probable", "onPartDie", "Cooked", "Confirmed", "onOverheat"), "High gee does not erase thermal evidence");
        Check(!Ledger.PreferEvidence("Stomped (1)", "Probable", "onCrash", "Life support failure", "Confirmed", "OtherMod"), "Custom cause stays authoritative");
        Check(Ledger.PreferCause("Overheating", "Confirmed", "Part destroyed", "Probable"), "Overheat overrides generic destruction");
        Check(!Ledger.PreferCause("Part destroyed", "Probable", "Overheating", "Confirmed"), "Generic destruction cannot replace overheat");
        Check(Ledger.PreferCause("Vessel collision", "Probable", "Crash", "Probable"), "Specific cause wins confidence tie");
        Check(!Ledger.PreferCause("Aerodynamic breakup", "Probable", "Ground impact", "Confirmed"), "Probable breakup cannot replace confirmed ground impact");
        Check(Ledger.PreferCause("Unloaded vessel terrain impact", "Confirmed", "Ground impact", "Confirmed"), "Unloaded impact is more specific");
        Check(!Ledger.PreferCause("Overheating", "Confirmed", "Life support failure", "Confirmed"), "Legacy confirmed custom cause protected");
        Check(Ledger.EvaCause("Ground impact", true) == "EVA ground impact", "EVA ground impact");
        Check(Ledger.EvaCause("Vessel collision", true) == "EVA collision", "EVA vessel collision");
        Check(Ledger.EvaCause("Collision", true) == "EVA collision", "EVA generic collision");
        Check(Ledger.EvaCause("Overheating", true) == "EVA overheating", "EVA heat");
        Check(Ledger.EvaCause("Water impact", true) == "EVA water impact", "EVA water");
        Check(Ledger.EvaCause("Overheating", false) == "Overheating", "Non-EVA unchanged");
        Check(Ledger.JointCause(100, 10, true) == "Aerodynamic breakup", "Airborne high pressure break");
        Check(Ledger.JointCause(100, 9.9, true) == "Structural failure", "Low pressure break");
        Check(Ledger.JointCause(100, 20, false) == "Structural failure", "Ground joint break not aerodynamic");
        Check(Ledger.JointCause(0, 20, true) == "Unknown", "No force joint removal ignored");
        Check(Ledger.JointCause(double.NaN, 20, true) == "Unknown", "Invalid force ignored");
        Check(Ledger.EvidenceInWindow(100, 105, 5), "Boundary evidence fresh");
        Check(!Ledger.EvidenceInWindow(100, 106, 1), "Game-time stale evidence ignored");
        Check(!Ledger.EvidenceInWindow(100, 101, 6), "Real-time stale evidence ignored");
        Check(!Ledger.EvidenceInWindow(100, 99, 1), "Reverted timeline evidence ignored");
        var contextA = new Context { VesselId = "A", PartId = "1" };
        Check(!Ledger.SameContext(contextA, new Context { VesselId = "B", PartId = "1" }), "Unrelated vessel rejected");
        Check(!Ledger.SameContext(contextA, new Context { VesselId = "A", PartId = "2" }), "Unrelated part rejected");
        Check(Ledger.SameContext(contextA, new Context { VesselId = "A" }), "Vessel-wide evidence accepted");
        Check(Ledger.MergeEvidence("onOverheat", "onPartDie") == "onOverheat | onPartDie", "Retain corroborating evidence");
        Check(Ledger.MergeEvidence("onOverheat | onPartDie", "onPartDie") == "onOverheat | onPartDie", "Deduplicate evidence");
        Check(Ledger.IsStockEvidence("onOverheat: part overheated | onPartDie"), "Recognize stock evidence");
        Check(!Ledger.IsStockEvidence("OtherMod"), "External source not stock");
        Check(new Casualty { ExternalCause = true }.Copy().ExternalCause, "Archive copy retains external cause flag");
        var deathQueue = new FatalityPlaqueQueue();
        Check(!deathQueue.Observe(c, false, false), "Returned MIA does not queue");
        Check(!deathQueue.Observe(d, false, true), "Imported death does not queue");
        Check(!deathQueue.Observe(a, true, false), "Already confirmed death does not queue");
        Check(deathQueue.Observe(a, false, false), "New confirmation queues one plaque");
        Check(!deathQueue.Observe(a, false, false), "Duplicate ID does not queue twice");
        Check(deathQueue.Observe(b, false, false) && deathQueue.Count == 2, "Two separate deaths queue");
        Check(deathQueue.Current == a.Id, "First death shown first");
        Check(!deathQueue.Acknowledge(b.Id) && deathQueue.Count == 2, "Cannot acknowledge a later plaque");
        Check(deathQueue.Acknowledge(a.Id) && deathQueue.Current == b.Id, "Close advances exactly one plaque");
        Check(!deathQueue.Observe(a, false, false), "Acknowledged ID cannot requeue");
        Check(deathQueue.Acknowledge(b.Id) && deathQueue.Count == 0 && deathQueue.Current == null, "Last plaque drains queue");
        var freshQueue = new FatalityPlaqueQueue();
        Check(freshQueue.Observe(a, false, false), "New timeline has independent queue");
        // No-repeat cycles survive reloads and do not depend on the record
        // continuing to display the message (e.g. a missing crew member returns).
        var cycle = new InscriptionCycle();
        var pool = new[] { "A", "B", "C", "A" };
        for (int round = 0; round < 4; round++)
        {
            var drawn = Enumerable.Range(0, 3).Select(i => cycle.Pick(pool)).ToArray();
            Check(drawn.Distinct().Count() == 3, "All unique messages used before next cycle");
        }
        var restoredCycle = new InscriptionCycle();
        restoredCycle.Restore(new[] { "A", "B" });
        Check(restoredCycle.Pick(new string[0]) == "" && restoredCycle.Used.Count == 2, "Empty pool does not reset cycle");
        Check(restoredCycle.Pick(pool) == "C", "Reload resumes unfinished cycle");
        Check(new[] { "A", "B", "C" }.Contains(restoredCycle.Pick(pool)) && restoredCycle.Used.Count == 1, "Exhaustion starts new cycle");
        var cycles = new InscriptionCycles();
        var legacy = new Casualty { Status = "Dead", Inscription = "A" };
        var legacyMissing = new Casualty { Status = "Missing", Inscription = "B", InscriptionForMissing = true, InscriptionFromMissingPool = true };
        cycles.Seed(new[] { legacy, legacyMissing });
        Check(cycles.Deceased.Used.SetEquals(new[] { "A" }) && cycles.Missing.Used.SetEquals(new[] { "B" }), "Seed old records in separate pools");
        Check(cycles.Deceased.Pick(new[] { "A", "B" }) == "B" && cycles.Missing.Pick(new[] { "A", "B" }) == "A", "Independent deceased and missing cycles");
        legacyMissing.ReturnUT = 99;
        Inscriptions.Assign(legacyMissing, null);
        Check(cycles.Missing.Used.Contains("B"), "Return does not forget used inscription");
        var archivedCycles = new InscriptionCycles();
        archivedCycles.CopyFrom(cycles);
        Check(archivedCycles.Missing.Used.SetEquals(cycles.Missing.Used) && archivedCycles.Deceased.Used.SetEquals(cycles.Deceased.Used), "Archive snapshot retains cycle state");
        Check(cycles.Deceased.Pick(new[] { "A", "B", "New" }) == "New", "Newly added messages used before restarting cycle");
        Check(!archivedCycles.Deceased.Used.Contains("New"), "Archive snapshot independent of later draws");
        var delayed = new FatalityPlaqueQueue();
        Check(!delayed.Observe(a, false, false, 10, false) && delayed.Count == 0, "Disabled automatic plaques do not queue");
        Check(delayed.Observe(a, false, false, 10, true) && !delayed.IsReady(11.499), "Plaque waits default 1.5 real seconds");
        Check(delayed.Observe(b, false, false, 10.5, true, 2.5) && !delayed.IsReady(11.999) && delayed.IsReady(12), "Second death restarts first plaque quiet interval");
        Check(delayed.Acknowledge(a.Id) && !delayed.IsReady(12.999) && delayed.IsReady(13), "Second plaque retains its own configured quiet interval");
        var quiet = new FatalityPlaqueQueue();
        quiet.NoteActivity(9);
        Check(quiet.Observe(a, false, false, 10, true, 1.5), "First death starts quiet interval");
        quiet.NoteActivity(11);
        quiet.NoteActivity(12);
        Check(!quiet.IsReady(13.499) && quiet.IsReady(13.5), "Every explosion restarts the pending deadline");
        quiet.NoteActivity(11);
        Check(!quiet.IsReady(13.499), "Older event cannot shorten quiet interval");
        Check(!quiet.TryStart(b.Id, 13.5) && !quiet.TryStart(a.Id, 13.499), "Only a ready head can start");
        Check(quiet.TryStart(a.Id, 13.5), "Ready head starts presentation");
        Check(quiet.Observe(b, false, false, 14, true, 1.5), "Later death queues while first plaque is visible");
        quiet.NoteActivity(15);
        Check(quiet.IsReady(15), "New accident cannot hide an already presented plaque");
        Check(!quiet.TryAdvanceForCrossfade(a.Id, 16.499), "Crossfade waits until last accident quiet interval ends");
        Check(quiet.TryAdvanceForCrossfade(a.Id, 16.5) && quiet.TryStart(b.Id, 16.5), "Crossfade starts once successor is ready");
        quiet.NoteActivity(17);
        Check(quiet.IsReady(17) && quiet.Acknowledge(b.Id), "Final visible plaque stays usable during later accidents");
        quiet.Clear();
        quiet.NoteActivity(20);
        Check(quiet.Count == 0 && !quiet.IsReady(21), "Accident without queued casualties creates no plaque");
        var crossfade = new FatalityPlaqueQueue();
        crossfade.Observe(a, false, false, 10, true, 0);
        Check(!crossfade.TryAdvanceForCrossfade(a.Id, 10) && crossfade.Count == 1, "Last plaque remains queued during fade-out");
        crossfade.Observe(b, false, false, 10, true, 2);
        Check(!crossfade.TryAdvanceForCrossfade(b.Id, 12) && crossfade.Current == a.Id, "Wrong plaque cannot crossfade");
        Check(!crossfade.TryAdvanceForCrossfade(a.Id, 11.999) && crossfade.Count == 2, "Crossfade honors next death deadline");
        Check(crossfade.TryAdvanceForCrossfade(a.Id, 12) && crossfade.Current == b.Id && crossfade.Count == 1, "Ready successor becomes queue head while old plaque fades");
        Check(!crossfade.TryAdvanceForCrossfade(a.Id, 13) && crossfade.Count == 1, "Repeated close cannot advance again");
        Check(!crossfade.TryAdvanceForCrossfade(b.Id, 13), "Final plaque cannot crossfade to absent successor");
        Check(crossfade.Acknowledge(b.Id) && crossfade.Count == 0, "Final acknowledgement drains queue");
        var immediate = new FatalityPlaqueQueue();
        Check(immediate.Observe(a, false, false, 10, true, 0) && immediate.IsReady(10), "Zero delay is immediately ready");
        immediate.NoteActivity(11);
        Check(immediate.IsReady(11), "Zero quiet interval remains immediate after an explosion");
        var maximum = new FatalityPlaqueQueue();
        Check(maximum.Observe(a, false, false, 10, true, 5) && !maximum.IsReady(14.999) && maximum.IsReady(15), "Maximum five-second delay");
        Check(FatalityPlaqueQueue.ClampDelay(-1) == 0 && FatalityPlaqueQueue.ClampDelay(8) == 5, "Invalid range is clamped");
        Check(FatalityPlaqueQueue.ClampDelay(double.NaN) == 1.5 && FatalityPlaqueQueue.ClampDelay(double.PositiveInfinity) == 5 && FatalityPlaqueQueue.ClampDelay(double.NegativeInfinity) == 0, "Non-finite saved delays are safe");
        Check(FatalityPlaqueQueue.DefaultFadeDuration == 2f, "Default fade is two seconds");
        Check(FatalityPlaqueQueue.ClampFadeDuration(0) == 0 && FatalityPlaqueQueue.ClampFadeDuration(5) == 5, "Fade includes both endpoints");
        Check(FatalityPlaqueQueue.ClampFadeDuration(1.7) == 1.7, "Custom fade duration retained");
        Check(FatalityPlaqueQueue.ClampFadeDuration(-1) == 0 && FatalityPlaqueQueue.ClampFadeDuration(8) == 5, "Invalid fade range clamped");
        Check(FatalityPlaqueQueue.ClampFadeDuration(double.NaN) == 2 && FatalityPlaqueQueue.ClampFadeDuration(double.PositiveInfinity) == 5 && FatalityPlaqueQueue.ClampFadeDuration(double.NegativeInfinity) == 0, "Non-finite saved fades are safe");
        delayed.Clear();
        Check(delayed.Count == 0 && !delayed.IsReady(99), "Disabling presentation clears pending plaques");
        Console.WriteLine(count + " assertions passed");
    }
}
