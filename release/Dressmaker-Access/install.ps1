$ErrorActionPreference = "Stop"
$ROOT = $PSScriptRoot
$FILES = Join-Path $ROOT "files"

function Say($m) { Write-Host $m }
function MD5($path) { (Get-FileHash -Algorithm MD5 -LiteralPath $path).Hash.ToUpper() }

Say ""
Say "===================================================="
Say " DRESSMAKER ACCESS  -  screen reader mod  -  INSTALL"
Say "===================================================="
Say ""
Say "This makes Dressmaker playable with the NVDA screen reader."
Say "It only ADDS files next to the game. It does not change the"
Say "game itself, and Uninstall puts everything back the way it was."
Say ""

# --- 1. Is the game running? ---------------------------------------------------
if (Get-Process -Name "Dressmaker" -ErrorAction SilentlyContinue) {
    Say "Dressmaker is currently running."
    Say "Please close the game completely, then run this installer again."
    Say ""
    Say "Nothing was changed."
    return
}

# --- 2. Locate the Dressmaker folder -------------------------------------------
$candidates = New-Object System.Collections.Generic.List[string]
$candidates.Add("C:\Program Files (x86)\Steam\steamapps\common\Dressmaker")
$candidates.Add("C:\Program Files\Steam\steamapps\common\Dressmaker")
try {
    foreach ($vdf in @("C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf",
                       "C:\Program Files\Steam\steamapps\libraryfolders.vdf")) {
        if (Test-Path $vdf) {
            foreach ($line in Get-Content $vdf) {
                if ($line -match '"path"\s+"(.+?)"') {
                    $p = $matches[1].Replace("\\","\")
                    $candidates.Add((Join-Path $p "steamapps\common\Dressmaker"))
                }
            }
        }
    }
} catch {}

$game = $null
foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c "Dressmaker.exe")) { $game = $c; break }
}
if (-not $game) {
    Say "I could not find Dressmaker automatically."
    Say ""
    Say "In Steam: right click Dressmaker, then Manage, then Browse local files."
    Say "Copy the folder path from the address bar."
    Say ""
    Say "Type or paste the full path to your Dressmaker folder"
    Say "(the folder containing Dressmaker.exe), then press Enter:"
    $game = (Read-Host "Folder").Trim('"').Trim()
}
if (-not $game -or -not (Test-Path (Join-Path $game "Dressmaker.exe"))) {
    Say ""
    Say "ERROR: that folder does not contain Dressmaker.exe. Nothing was changed."
    return
}
Say "Found Dressmaker at: $game"

# --- 3. Already installed? -----------------------------------------------------
$rel = "BepInEx\plugins\DressmakerAccess\DressmakerAccess.dll"
$installed = Join-Path $game $rel
$new = Join-Path $FILES $rel
if (Test-Path $installed) {
    if ((MD5 $installed) -eq (MD5 $new)) {
        Say ""
        Say "Good news: this version is already installed. Nothing to do."
        Say ""
        Say "Make sure NVDA is running, then launch Dressmaker from Steam."
        return
    }
    Say ""
    Say "A different version of the mod is already installed - updating it."
}

# --- 4. Copy everything in -----------------------------------------------------
Say ""
Say "Installing..."
Copy-Item -Path (Join-Path $FILES "*") -Destination $game -Recurse -Force
$doorstopVersion = Join-Path $FILES ".doorstop_version"
if (Test-Path $doorstopVersion) {
    Copy-Item -LiteralPath $doorstopVersion -Destination $game -Force
}

# --- 5. Check it landed --------------------------------------------------------
$ok = $true
foreach ($f in @("winhttp.dll", "doorstop_config.ini", "nvdaControllerClient64.dll",
                 "BepInEx\core\BepInEx.dll", $rel)) {
    if (-not (Test-Path (Join-Path $game $f))) { Say "  MISSING: $f"; $ok = $false }
}
if (-not $ok) {
    Say ""
    Say "ERROR: some files did not copy. Try running this installer again by"
    Say "right clicking Install and choosing Run as administrator."
    return
}

Say ""
Say "===================================================="
Say " DONE!  Dressmaker Access is installed."
Say "===================================================="
Say ""
Say "Before you play: make sure NVDA is running."
Say "Then launch Dressmaker from Steam as normal."
Say ""
Say "A few seconds after it opens you should hear:"
Say "     Dressmaker Access loaded. Press H at any time to hear what the keys do."
Say "If you hear that, it worked."
Say ""
