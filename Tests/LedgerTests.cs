using System;
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
        Console.WriteLine(count + " assertions passed");
    }
}
