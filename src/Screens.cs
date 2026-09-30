using UnityEngine.SceneManagement;

namespace DressmakerAccess
{
    /// <summary>
    /// What each screen is and which keys do what. A short version is spoken on
    /// arriving; H (or F1) gives the full version any time.
    /// </summary>
    internal static class Screens
    {
        private const string Everywhere =
            "Everywhere: arrows or Tab move, Enter chooses. S jumps between the room and the sidebar, back to where you were in each. H is this help, M tells you your money and commission, R repeats. " +
            "Number keys go to rooms: 1 front desk, 2 measuring, 3 sketchbook, 4 fabric shop, 5 cutting table, 6 mannequin, 7 sewing machine. " +
            "Backspace closes a popup or goes back; Escape closes a popup, or opens the options. F4 reads everything on screen. F7 gives this room's details.";

        /// <summary>Spoken just after the room name when you arrive.</summary>
        internal static string Intro(GameManager.Scene s)
        {
            switch (s)
            {
                case GameManager.Scene.FrontDesk:
                    return "Your shop's front desk. Ring the bell for your next customer. Arrows move, Enter chooses, number keys go to rooms, H for help.";
                case GameManager.Scene.Sketchbook:
                    return "Design the dress here. Arrows move through the parts; Enter on Next or Previous changes them. Page Up and Page Down turn the pages. T tells you how the client's wishes are met. H for help.";
                case GameManager.Scene.Store:
                    return "Up and Down browse the shelf, Enter takes a fabric down. Left and Right reach the buy panel and filter. H for help.";
                case GameManager.Scene.CuttingRoom:
                    return "Choose a fabric, then lay out and cut your pattern pieces. H for the keys.";
                case GameManager.Scene.Mannequin:
                    return "Enter on a pattern piece puts it on the mannequin; seams ready to sew appear in the list. S jumps between the mannequin and the sidebar. H for help.";
                case GameManager.Scene.Photo:
                    return "Take a photo of your dress, then hand it over. Arrows move, Enter chooses. H for help.";
                case GameManager.Scene.Sewing:
                    // Arriving from a seam announces the seam itself; arriving here directly means nothing to sew.
                    return Sewing.InSeam ? null
                        : "Nothing to sew from here. Seams are started from the mannequin: press 6, put your cut pieces on, then choose a Sew seam item.";
            }
            return null; // measuring and sewing announce their own instructions
        }

        internal static string For(GameManager.Scene s)
        {
            switch (s)
            {
                case GameManager.Scene.FrontDesk:
                    return "Front desk. Ring the bell to bring in your next customer. From here you can go to every room with the number keys.";
                case GameManager.Scene.Measuring:
                    return "Measuring your client. Up and Down slide the tape measure a little at a time, Shift for bigger steps; Page Up and Page Down jump between the bust, waist and hips lines. " +
                           "It tells you the body area, the measurement, and whether you're on the guide line. Hold still on each line to record it. Then Tab to Confirm. F7 lists what you've measured.";
                case GameManager.Scene.MannequinSizing:
                    return "Sizing the mannequin to your client. Up and Down go between the bust, waist and hips lines; Left and Right turn that line's knob, Shift for bigger turns. " +
                           "It tells you the mannequin's size, the client's, and which way to turn. F7 compares all three. Tab to Confirm when they match.";
                case GameManager.Scene.Sketchbook:
                    return "Sketchbook. Arrows or Tab move through the design: Next and Previous for bodice, collar, sleeves and skirt, and their styles. " +
                           "Fabrics in the sidebar: Enter puts a swatch on the sketch so its styles count; Enter on a swatch removes it. " +
                           "T reads how your design meets the client's wishes. The pencil colours the drawing only. Draft Pattern when you're happy. " +
                           "Page Up and Page Down turn the pages: back to your finished dresses and the friendship book, forward through the dresses in progress, commissions and off-the-rack designs, to a page for starting a new off-the-rack design. Each page says what it is. The dress in progress you turn to is the one you then work on in every room.";
                case GameManager.Scene.Store:
                    return "Fabric shop. Up and Down browse the shelf; Page Up and Page Down jump ten; Home and End go to the ends; a letter jumps to names starting with it, Shift with H or M for those letters. " +
                           "Each fabric says its price and styles, the ones your client wants first. Enter takes it down to see everything about it. " +
                           "Left, Right or Tab reach the buy panel: Amount, then Buy. The Filter button narrows the shelf.";
                case GameManager.Scene.CuttingRoom:
                    return "Cutting table. In the sidebar, the Fabric tab: Enter puts a fabric on the table. The Patterns tab: Enter lays a piece on the fabric in the first free space. " +
                           "New pieces start at the far end. While holding an uncut piece: Q and E turn it, Shift for a quarter turn; two tones play, and they come together into one clear note when the grain is straight, the best, or settle into a calm chord on cross grain, second best; clashing means off. " +
                           "Arrows slide the piece until it bumps into something, and say what; Shift with an arrow nudges it a little. Slide pieces against each other and the used end to save fabric. C cuts it out, Backspace puts it back. " +
                           "Shortcuts if you just want to get on: G straightens the grain for you, F moves the piece to the tightest free space. " +
                           "Pieces on the fabric are in the list; Enter picks one up. X trims off the fabric you've used so more unrolls. Shift Backspace puts all cut pieces back. F7 says what's on the table.";
                case GameManager.Scene.Mannequin:
                    return "Mannequin. Patterns tab: Enter on a cut piece puts it in its place on the body. When two neighbouring pieces are on, their seam appears in the list as Sew seam; Enter starts sewing it. " +
                           "Seam Rip undoes a seam. The Accessories tab is for trims and buttons. S jumps between the mannequin, with its seams, and the sidebar, with the pieces and accessories; it remembers where you were in each.";
                case GameManager.Scene.Sewing:
                    return "Sewing machine. Hold Space to sew. Up and Down change speed. With sewing assist on, the machine steers for you. " +
                           "With it off, Left and Right arrows steer: a hum sounds from the side to steer towards, and gets higher as you near the edge; silence means you're on course. " +
                           "R restarts the seam. F7 progress and accuracy. F5 sewing assist on or off, F6 the hum on or off.";
                case GameManager.Scene.Photo:
                    return "Photo studio. The finished dress is on show. The camera buttons (rotate, zoom, focus) move the view a little each press, and there are choices for background, light and viewfinder. " +
                           "Take Photo, then Save or Retake. Then choose: Complete Commission hands the dress to your client, Keep working takes it back to the mannequin, Sell sells it instead.";
            }
            return "";
        }

        internal static void Help()
        {
            string what;
            if (Dialogue.OptionsActive)
                what = "A conversation choice. Up and Down go through the answers, Enter picks one, or press its number. F2 repeats.";
            else if (Dialogue.LineActive)
                what = "A conversation. Enter goes to the next line. F2 repeats the last line.";
            else
            {
                var gm = Rooms.GameManagerOrNull();
                if (gm == null)
                    what = SceneManager.GetActiveScene().name == "TitleScreen"
                        ? "Title screen. Arrows or Tab move through Play, Options and Exit; Enter chooses. Play lets you pick a save slot."
                        : "";
                else
                    what = For(gm.CurrentScene);
            }
            Speech.Say(what + " " + Everywhere);
        }
    }
}
