====================================================================
 DRESSMAKER ACCESS  -  play Dressmaker with a screen reader
 Version 0.9.2 (beta)
====================================================================

An unofficial accessibility mod for Dressmaker (Steam) that makes the
whole dressmaking loop playable by ear with NVDA: talking to clients,
measuring, designing, buying fabric, cutting, sewing, accessories, and
handing the dress over.

It is a BETA. The main game works from start to a finished, delivered
dress, and it has been played that way by a blind player. Some side
screens are untested (see "Not done yet" near the end). Please tell us
what breaks.


--------------------------------------------------------------------
 INSTALL
--------------------------------------------------------------------

  1. Close Dressmaker.
  2. Run Install.bat in this folder. It finds the game by itself and
     copies the mod in. If it can't find the game it asks for the
     folder: in Steam, right click Dressmaker, Manage, Browse local
     files, and copy the path from the address bar.
  3. Start NVDA.
  4. Launch Dressmaker from Steam. After a few seconds you hear:
     "Dressmaker Access loaded. Press H at any time to hear what the
     keys do."

To remove it: run Uninstall.bat. Your saves are never touched.

Uses BepInEx 5 (included). If you already use other BepInEx mods for
Dressmaker, the installer adds to them and the uninstaller leaves the
framework in place.


--------------------------------------------------------------------
 KEYS YOU CAN USE EVERYWHERE
--------------------------------------------------------------------

  Arrows or Tab / Shift Tab   move through what's on screen: first the
                              room itself, then the sidebar, then Back
                              and Settings
  S                           jump between the room and the sidebar,
                              back to where you were in each (not at
                              the sewing machine, where S is the game's)
  Enter                       choose
  H                           help: what this screen is, and its keys
  M                           your money, rank, the client's wishes,
                              and the current dress's quality
  R                           repeat the last thing said
                              (at the sewing machine R restarts the
                              seam instead; use F2 there)
  1 to 7                      go to a room: 1 front desk, 2 measuring,
                              3 sketchbook, 4 fabric shop, 5 cutting
                              table, 6 mannequin, 7 sewing machine
                              (rooms the game hasn't opened yet stay shut)
  Backspace                   close a popup, list or the options, or
                              go Back from a room (at the cutting
                              table it puts a held piece back)
  Escape                      close a popup; with none open, the
                              game's options menu as usual
  F4                          read everything on the screen
  F7                          this room's details
  F5 / F6                     sewing assist on/off / steering hum on/off

Every room says its name and a one-line reminder when you arrive, and
every tutorial tip ends with how to do that step with this mod.


--------------------------------------------------------------------
 CONVERSATIONS
--------------------------------------------------------------------

Each line is read with the speaker's name. Enter goes to the next line.
When you have to answer, the choices are read out: Up and Down go
through them and Enter picks one, or press its number.


--------------------------------------------------------------------
 MEASURING YOUR CLIENT
--------------------------------------------------------------------

Up and Down slide the tape measure (Shift for bigger steps). Page Up
and Page Down jump straight to the bust, waist and hips lines. It tells
you the body area, the measurement, and whether you're on the guide
line. A measurement only records when you hold still ON the line, so
passing through an area can't spoil a good one. Then Tab to Confirm.


--------------------------------------------------------------------
 SIZING THE MANNEQUIN
--------------------------------------------------------------------

Up and Down go between the bust, waist and hips lines. Left and Right
turn that line's knob (Shift for bigger turns). Each turn says the
mannequin's size, the client's, and "too big, turn Left", "too small,
turn Right" or "matched". F7 compares all three and gives the fit.


--------------------------------------------------------------------
 SKETCHBOOK (designing)
--------------------------------------------------------------------

Next and Previous for bodice, collar, sleeves and skirt, and their
styles. "Choose from a list" opens every option with its styles (the
ones your client wants first); the last item closes the list.
Fabrics in the sidebar: Enter puts a swatch on the sketch so its styles
count in the estimate. T reads how your design meets the client's
wishes. Draft Pattern finishes the design.

Page Up and Page Down turn the sketchbook's pages, and each page says
what it is. From front to back: the friendship book (your clients),
your finished dresses, the dresses in progress (commissions and
off-the-rack designs, "2 of 3" and so on), and, once the game unlocks
it, a page for starting a new off-the-rack design. The dress in
progress you turn to is the one you then work on in every room, so you
can start your own design while a commission waits, and come back to
it. M says which dress you're on.

The "Your dress so far" box scores the REAL dress as you cut and sew
it, so it starts at zero; the estimate while designing is T.


--------------------------------------------------------------------
 FABRIC SHOP
--------------------------------------------------------------------

Up and Down browse the shelf, Page Up/Down jump ten, Home/End go to the
ends, a letter jumps to names starting with it (hold Shift for H, M and
R). Each item says its price and styles, the client's wanted ones
first. Enter takes it down; Left, Right or Tab reach the buy panel:
Amount, then Buy. "Shop shelf: show accessories" switches to buttons,
bows and trims (trims are sold by the metre).


--------------------------------------------------------------------
 CUTTING TABLE
--------------------------------------------------------------------

Sidebar, Fabric tab: Enter puts that fabric on the table.
Sidebar, Patterns tab: Enter lays a piece at the far end of the fabric.

While holding an uncut piece:
  Q / E        turn it one notch; Shift turns a quarter
               Two tones play after each turn. Clashing means the grain
               is off; a calm chord means cross grain (second best);
               one clear note means straight grain, the best ("Spot on"
               at 100%). Bias pieces: the clear note is right for them too.
  Arrows       slide it until it bumps something, and say what
               ("Touching Skirt Back", "the bottom edge of the fabric")
  Shift+arrow  nudge it a little
  C            cut it out (then it says how much fabric you've used)
  Backspace    put it back in the sidebar
  G / F        shortcuts if you just want to get on: G straightens the
               grain for you, F moves it to the tightest free space

Other keys: pieces on the fabric are in the list (Enter picks one up);
X trims off the used fabric so more unrolls; Shift+Backspace puts
every cut piece back in the sidebar.

Tip: slide each piece down, then left, to pack it against the last one.


--------------------------------------------------------------------
 MANNEQUIN
--------------------------------------------------------------------

Patterns tab: Enter on a cut piece puts it in its place on the body.
When neighbouring pieces are on, "Sew seam" items appear in the list,
with where the seam is ("your left", "on the back"); Enter starts it.
Seams are always started from here, not from the sewing table.
S jumps between the mannequin (its seams) and the sidebar (the pieces
and accessories). After putting a piece on, Tab goes to the next one.

Accessories tab: Enter picks an accessory up. Then:
  - buttons, bows, flowers...: choose a piece of the dress, then a spot
    on it (top/middle/bottom, left/centre/right)
  - trims (lace, piping...): choose a seam to run it along
  - the last choice always puts it back
A placed item stays selected with the game's size and turn buttons,
plus Done and Remove. With nothing held, what's already on the dress is
listed; Enter selects one.
Where things go is your choice: clients only count how many and what kind.

When every seam is sewn, Finish Dress takes you to the photo studio.


--------------------------------------------------------------------
 SEWING MACHINE
--------------------------------------------------------------------

Hold Space to sew. Up and Down change speed. R restarts the seam.
With the game's sewing assist on (the mod switches it on the first
time), the machine steers for you. Turn it off with F5 to steer
yourself with Left and Right: a hum sounds from the side to steer
towards and rises in pitch as you near the edge; silence means you're
on course. Progress is read every quarter; F7 gives progress, accuracy
and speed. Going off the line rewinds to your last good stitch.


--------------------------------------------------------------------
 PHOTO STUDIO AND HANDING OVER
--------------------------------------------------------------------

Camera buttons move the view a little per press. Take Photo, then
choose Complete Commission to hand the dress over (or Keep working, or
Sell). "Save" in that dialog is optional: it saves the photo as a
picture file and opens a Windows save window (Escape cancels). Export
and Import also open Windows file windows; you never need them.
After the client's reaction, new unlocks are announced: press Enter to
see them.


--------------------------------------------------------------------
 NOT DONE YET (help us test)
--------------------------------------------------------------------

  - Gossip newspaper and letters: text is read, page turning untested.
  - Selling or gifting a dress instead of handing it over: untested.
  - Free designs with no client, the friendship pages, past dresses,
    the mannequin colour picker, filter popups, the options sliders:
    untested.
  - Late game and the ending: never reached yet.
  - Colouring the sketch with the pencil lands in the wrong places.
  - A trim follows one seam at a time.
  - The mod's own words are English only.

Report problems and ideas on the GitHub page (Issues). Including the
file Dressmaker\BepInEx\LogOutput.log helps a lot: it records what the
mod said and did.


--------------------------------------------------------------------
 CREDITS
--------------------------------------------------------------------

Dressmaker is made by Cozy Lives; this mod is unofficial and not
affiliated with them. Please support the game.
Mod by Lilian Coghlan, built with Claude. Uses BepInEx (LGPL-2.1) and
the NVDA Controller Client (LGPL-2.1).
====================================================================
