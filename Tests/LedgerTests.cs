using System;
using System.Linq;
using System.IO;
using KerbalMemorial;
internal static class LedgerTests
{
    private static int count;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
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
        var c = l.Observe("Bob", "Missing", 40); l.Observe("Bob", "Assigned", 50);
        Check(c.DisplayStatus == "Returned" && !c.Fatal, "Ordinary missing return");
        var d = l.Observe("Bill", "Dead", 60, false, true);
        Check(d.Fatal && d.UT == -1 && d.DeathUT == -1, "Imported death unknown date");
        Ledger.Cause(d, "Crash", "Probable", "event"); Ledger.Cause(d, "Impact", "Confirmed", "event"); Ledger.Cause(d, "Unknown", "Unknown", "event");
        Check(d.Cause == "Impact", "Cause confidence priority");
        d.Location = new Context { Vessel = "Test" }; var copy = d.Copy(); copy.Location.Vessel = "Other";
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
        var noise = new int[flat.Length]; var stripes = new int[flat.Length];
        var face = new int[flat.Length]; var random = new Random(123);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int i = y * 64 + x;
                flat[i] = 300; noise[i] = random.Next(766);
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
        Func<string> choose = () => { picks++; return "Among the stars."; };
        Check(!Inscriptions.Assign(loss, choose) && picks == 0, "No inscription for unconfirmed missing crew");
        loss.ReportedKilled = true;
        Check(Inscriptions.Assign(loss, choose) && picks == 1, "Assign at first fatality confirmation");
        Check(!Inscriptions.Assign(loss, choose) && picks == 1, "Duplicate death does not reselect");
        Check(!Inscriptions.Assign(loss, choose) && picks == 1, "Existing inscription never replaced");
        Check(Inscriptions.Display(loss) == "Among the stars." && loss.Notes == "Old note", "Inscription independent of notes");
        Check(Inscriptions.Display(new Casualty { Notes = "Legacy text" }) == "Legacy text", "Legacy note remains visible");
        Check(loss.Copy().Inscription == loss.Inscription, "Archive copy retains inscription");
        var imported = new Casualty { Status = "Dead", Imported = true, UT = -1 };
        Check(Inscriptions.Assign(imported, choose), "Historical imports receive inscriptions");
        Check(!Inscriptions.Assign(imported, choose), "Backfill is idempotent");
        var legacy = new Casualty { Status = "Dead", ReturnUT = 100, Inscription = " " };
        Check(Inscriptions.Assign(legacy, choose), "Previously respawned fatality receives inscription");
        var current = new Casualty { Status = "Dead" };
        var archived = new Casualty { Status = "Dead", Inscription = "Existing archive message" };
        Check(!Inscriptions.FillPair(current, archived, () => { throw new Exception("Unexpected pick"); }) && current.Inscription == archived.Inscription, "Restore archived inscription without reselection");
        archived.Inscription = "";
        Check(Inscriptions.FillPair(current, archived, () => { throw new Exception("Unexpected pick"); }) && archived.Inscription == current.Inscription, "Fill archive from current record");
        current.Inscription = archived.Inscription = "";
        int batchPicks = 0;
        Check(Inscriptions.FillPair(current, archived, () => { batchPicks++; return "Shared message"; }) && batchPicks == 1 && current.Inscription == archived.Inscription, "One selection for both copies");
        var retry = new Casualty { Status = "Dead" };
        Check(!Inscriptions.Assign(retry, () => "") && Inscriptions.Assign(retry, choose), "Retry after empty pool");
        Check(!Inscriptions.Assign(new Casualty { Status = "Dead" }, () => ""), "Empty pool does not assign");
        string directory = Path.Combine(Path.GetTempPath(), "KerbalMemorial-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(Inscriptions.Pick(directory) == "", "Missing directory is safe");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "a.txt"), new[] { "### **Short plaque inscriptions**", "- First message" });
            File.WriteAllLines(Path.Combine(directory, "b.TXT"), new[] { "### **Other inscriptions**", "1. Second message" });
            File.WriteAllText(Path.Combine(directory, "README.md"), "Not an inscription");
            Check(Enumerable.Range(0, 20).All(i => new[] { "First message", "Second message" }.Contains(Inscriptions.Pick(directory))), "Only valid text file messages selected");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine(count + " assertions passed");
    }
}
