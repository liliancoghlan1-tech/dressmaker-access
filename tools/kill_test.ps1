# Stop ONLY the test copy of Dressmaker (never Lilian's real install).
Get-Process Dressmaker -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like '*DressmakerTest*' } |
    Stop-Process -Force
Start-Sleep -Milliseconds 800
