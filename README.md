# Dressmaker Access

An unofficial screen-reader accessibility mod for **[Dressmaker](https://store.steampowered.com/app/4019220/Dressmaker/)** (Steam), so blind and low-vision players can run the whole shop by ear with **NVDA**.

**Status: 0.9.1 beta.** The full loop has been played blind from start to a delivered dress: taking the commission, measuring the client, designing in the sketchbook, buying fabric, cutting, sewing, accessories, the photo, and handing it over. Some side screens are still untested; see *Not done yet*. Reports are very welcome.

## Install

1. Download `Dressmaker-Access.zip` from [Releases](../../releases) and unzip it.
2. Close Dressmaker, then run **Install.bat**. It finds the game itself (or asks for the folder).
3. Start NVDA and launch Dressmaker from Steam. You'll hear *"Dressmaker Access loaded. Press H at any time to hear what the keys do."*

**Uninstall.bat** removes it again. Saves are never touched. BepInEx 5 is included.

## What it does

- **Everywhere:** arrows or Tab move through what's on screen, Enter chooses. **H** is help for the screen you're on, **M** gives your money and the client's wishes, **R** repeats, and **1-7** jump between rooms. Every room announces itself and its keys, and every tutorial tip ends with how to do that step with the mod.
- **Conversations:** lines are read with the speaker's name. Choices are read out; pick one with Up/Down + Enter or its number.
- **Measuring:** slide the tape by ear, jump to the bust/waist/hips lines, and it only records on the line. **Mannequin sizing:** go line to line and turn each knob until it says *matched*.
- **Sketchbook:** every part and style is named, the "choose from a list" picker reads each garment's styles, fabric swatches can be placed, and **T** reads how the design meets the client's wishes.
- **Shop:** browse the shelf with Up/Down, Page Up/Down and letter jumps. Items read their price and styles, the client's wanted ones first. Buy panel, and the accessories shelf.
- **Cutting:** pieces are laid on the fabric. **Tune the grain by ear** with Q/E (two tones meet in one clear note when it's straight), **slide** pieces with the arrows until they bump something, then cut, trim the used fabric, and repeat. There are optional G/F shortcuts.
- **Mannequin:** put pieces on with Enter; seams appear as a list with where they are on the body.
- **Sewing:** hold Space. The game's sewing assist is on by default. Or turn it off and steer by a hum that sounds from the side to steer towards. Progress and accuracy are spoken.
- **Accessories:** pick a piece of the dress and a spot on it; trims run along a seam of your choice. Placed items can be adjusted or removed.
- **Photo studio and hand-over:** every button and dialog is read, and unlocks are announced.

The full guide with every key is in `README.txt` inside the download.

## Not done yet

- Gossip newspaper and letters: the text is read, but page turning is untested.
- Selling or gifting a dress: untested.
- Free designs with no client, the friendship pages, past dresses, the mannequin colour picker, filter popups and the options sliders: untested.
- The late game and the ending: not reached yet.
- The sketch pencil colours the wrong areas.
- A trim follows one seam at a time.
- The mod's own speech is English only.

## Reporting problems

Open an [Issue](../../issues). Attaching `Dressmaker\BepInEx\LogOutput.log` helps a lot: it records everything the mod said and did.

## Building

Source is in `src/` (C#, netstandard2.1, BepInEx 5 + Harmony). `DressmakerAccess.csproj` references the game's `Dressmaker_Data\Managed` folder and a BepInEx install via `GameDir`. `dotnet build -c Release`.

## Credits

Dressmaker is made by Cozy Lives. This mod is unofficial and not affiliated with them. Please support the game.
Mod by Lilian Coghlan, built with Claude. Uses [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and the NVDA Controller Client (LGPL-2.1). Mod code: MIT licence.
