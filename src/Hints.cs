namespace DressmakerAccess
{
    /// <summary>
    /// The game's tips talk about dragging and clicking. After each tip, say how to
    /// do the same thing with this mod's keys.
    /// </summary>
    internal static class Hints
    {
        internal static string ForTip(string message)
        {
            string m = (message ?? "").ToLowerInvariant();
            var gm = Rooms.GameManagerOrNull();
            var scene = gm != null ? gm.CurrentScene : (GameManager.Scene)(-1);

            if (scene == GameManager.Scene.MannequinSizing && (m.Contains("gear") || m.Contains("ghost")))
                return " With the mod: Up and Down go between the lines, Left and Right turn that line's knob.";
            if ((scene == GameManager.Scene.Measuring || scene == GameManager.Scene.MannequinSizing) && m.Contains("tape"))
                return scene == GameManager.Scene.MannequinSizing ? " With the mod: Up and Down go between the lines." : " With the mod: Up and Down arrows move the tape; Page Up and Page Down jump between lines.";
            if (scene == GameManager.Scene.CuttingRoom)
            {
                if (m.Contains("select the fabric")) return " With the mod: Tab to a fabric in the sidebar and press Enter.";
                if (m.Contains("switch to patterns")) return " With the mod: Tab to Patterns and press Enter.";
                if (m.Contains("drag your pattern")) return " With the mod: Tab to a pattern piece and press Enter to lay it on the fabric.";
                if (m.Contains("rotation knob") || m.Contains("rotate")) return " With the mod: Q and E turn the piece; listen for the two tones to come into one clear note. Shift turns a quarter.";
                if (m.Contains("discard")) return " With the mod: press X to trim off the used fabric.";
                if (m.Contains("scissors")) return " With the mod: with a piece picked up, press C.";
                if (m.Contains("right click") || m.Contains("back into your inventory")) return " With the mod: Tab to the piece on the fabric, press Enter to pick it up, then Backspace. Shift Backspace puts every cut piece back at once.";
                if (m.Contains("discard") || m.Contains("used fabric")) return " With the mod: press X to trim off the used fabric.";
                if (m.Contains("rest of the pieces")) return " With the mod: for each piece, Enter lays it at the far end, Q and E tune the grain, arrows slide it into place, C cuts.";
            }
            if (scene == GameManager.Scene.Sewing)
            {
                if (m.Contains("rotate")) return " With the mod: Left and Right arrows turn it, towards the hum. With sewing assist on, the machine steers for you. F5 switches the assist.";
            }
            if (scene == GameManager.Scene.Mannequin)
            {
                if (m.Contains("drag the items") || m.Contains("accessor")) return " With the mod: Tab to something on the dress in the list and press Enter to select it; then Remove, or its size and turn buttons.";
                if (m.Contains("drag")) return " With the mod: Tab to a pattern piece in the sidebar and press Enter to put it on.";
                if (m.Contains("sew")) return " With the mod: Tab to a Sew seam item and press Enter.";
            }
            if (scene == GameManager.Scene.Sketchbook)
            {
                if (m.Contains("arrow")) return " With the mod: Tab to Next bodice, Next skirt and so on, and press Enter.";
                if (m.Contains("drag fabric") || m.Contains("fabric onto")) return " With the mod: Tab to a fabric in the sidebar and press Enter to put a swatch on the sketch.";
                if (m.Contains("pencil")) return " With the mod: Tab to Pencil and press Enter.";
                if (m.Contains("tag")) return " Press T to hear them.";
                if (m.Contains("draft")) return " With the mod: Tab to Draft Pattern.";
                if (m.Contains("find a design")) return " Press T to check how you're doing.";
                if (m.Contains("non commissioned") || m.Contains("new dress")) return " With the mod: Page Down to the last page, or Tab to New Dress, then Create New Dress.";
            }
            return "";
        }
    }
}
