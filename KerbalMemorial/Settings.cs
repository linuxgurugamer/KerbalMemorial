namespace KerbalMemorial
{
    public class Settings : GameParameters.CustomParameterNode
    {
        public override string Title => "Kerbal Memorial";
        public override string Section => "Kerbal Memorial";
        public override string DisplaySection => Section;
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;
        [GameParameters.CustomParameterUI("Keep deaths across reverts", toolTip = "Archive incidents outside the save. Archived incidents never change the current roster.")]
        public bool permanentHistory = false;
        [GameParameters.CustomParameterUI("Show casualty notifications", toolTip = "Show a message for newly detected incidents, not historical imports.")]
        public bool notifications = true;
        [GameParameters.CustomParameterUI("Import existing casualties", toolTip = "Record already dead or missing crew when the tracker first sees them. Dates may be unknown.")]
        public bool importExisting = true;
    }
}
