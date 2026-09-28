$ErrorActionPreference = "Stop"

function Say($m) { Write-Host $m }

Say ""
Say "===================================================="
Say " DRESSMAKER ACCESS  -  screen reader mod  -  REMOVE"
Say "===================================================="
Say ""

if (Get-Process -Name "Dressmaker" -ErrorAction SilentlyContinue) {
    Say "Dressmaker is currently running."
    Say "Please close the game completely, then run this again."
    Say ""
    Say "Nothing was changed."
    return
}

$candidates = New-Object System.Collections.Generic.List[string]
$candidates.Add("C:\Program Files (x86)\Steam\steamapps\common\Dressmaker")
$candidates.Add("C:\Program Files\Steam\steamapps\common\Dressmaker")
try {
    foreach ($vdf in @("C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf",
                       "C:\Program Files\Steam\steamapps\libraryfolders.vdf")) {
        if (Test-Path $vdf) {
            foreach ($line in Get-Content $vdf) {
                if ($line -match '"path"\s+"(.+?)"') {
                    $candidates.Add((Join-Path $matches[1].Replace("\\","\") "steamapps\common\Dressmaker"))
                }
            }
        }
    }
} catch {}

$game = $null
foreach ($c in $candidates) { if (Test-Path (Join-Path $c "Dressmaker.exe")) { $game = $c; break } }
if (-not $game) {
    Say "Type or paste the full path to your Dressmaker folder, then press Enter:"
    $game = (Read-Host "Folder").Trim('"').Trim()
}
if (-not $game -or -not (Test-Path (Join-Path $game "Dressmaker.exe"))) {
    Say "Folder not found. Nothing changed."
    return
}
Say "Found Dressmaker at: $game"

# Our own files first: the mod, its settings, and the speech library.
$ours = Join-Path $game "BepInEx\plugins\DressmakerAccess"
if (Test-Path $ours) { Remove-Item -LiteralPath $ours -Recurse -Force; Say "Removed the mod" }
$cfg = Join-Path $game "BepInEx\config\lilian.dressmakeraccess.cfg"
if (Test-Path $cfg) { Remove-Item -LiteralPath $cfg -Force; Say "Removed the mod's settings" }
$nvda = Join-Path $game "nvdaControllerClient64.dll"
if (Test-Path $nvda) { Remove-Item -LiteralPath $nvda -Force; Say "Removed the NVDA speech library" }

# BepInEx is shared. If another mod uses it, leave it alone.
$plugins = Join-Path $game "BepInEx\plugins"
$othersRemain = $false
if (Test-Path $plugins) {
    $left = Get-ChildItem $plugins -Recurse -File -ErrorAction SilentlyContinue
    if ($left -and $left.Count -gt 0) { $othersRemain = $true }
}

if ($othersRemain) {
    Say ""
    Say "Other BepInEx mods are still installed, so the framework has been left in place."
    Say "Dressmaker Access itself is removed."
} else {
    foreach ($f in @("BepInEx", "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt")) {
        $p = Join-Path $game $f
        if (Test-Path $p) {
            Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue
            Say "Removed $f"
        }
    }
    Say ""
    Say "The accessibility mod and its framework have been removed."
}
Say "Your saves are not touched."
Say ""
