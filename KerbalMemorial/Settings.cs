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
        [GameParameters.CustomParameterUI("Show memorial plaque on death", toolTip = "Show each newly confirmed fatality plaque after the configured delay. Off leaves stock Flight Results unchanged.")]
        public bool showDeathPlaques = true;
        [GameParameters.CustomFloatParameterUI("Death plaque delay (seconds)", minValue = FatalityPlaqueQueue.MinDelay, maxValue = FatalityPlaqueQueue.MaxDelay, stepCount = 51, displayFormat = "F1", toolTip = "Real-time seconds after a confirmed death before its plaque can appear. Zero means no intentional delay.")]
        public float deathPlaqueDelay = FatalityPlaqueQueue.DefaultDelay;
        [GameParameters.CustomParameterUI("Import existing casualties", toolTip = "Record already dead or missing crew when the tracker first sees them. Dates may be unknown.")]
        public bool importExisting = true;
        [GameParameters.CustomParameterUI("Use Alternate memorial wall theme", toolTip = "Off selects Classic; on selects the industrial Alternate wall.")]
        public bool alternateWallTheme = false;
        [GameParameters.CustomParameterUI("Hide theme button on memorial wall", toolTip = "The theme can still be selected from the Memorial Settings / Help tab.")]
        public bool hideWallThemeButton = false;
    }
}
