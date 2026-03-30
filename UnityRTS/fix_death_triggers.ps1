# Fix death-trigger Update() -> FixedUpdate() in PlayMode tests
# Death processing moved to FixedUpdate; tests must call FixedUpdate to trigger it.

function ReplaceDeathTrigger {
    param([string]$filePath, [string]$unitVar)
    $content = [System.IO.File]::ReadAllText($filePath)
    $before = $content.Length

    # Pattern: <unitVar>.Health = <value>;\r\n\t+<unitVar>.Update();
    # Replace only the .Update() line that immediately follows a Health = ... assignment
    # We use a regex that matches the specific death-trigger pattern
    $pattern = "(\t+$unitVar\.Health\s*=\s*[^;]+;\r\n\t+)$unitVar\.Update\(\);"
    $replacement = "`${1}$unitVar.FixedUpdate();"
    $content = [regex]::Replace($content, $pattern, $replacement)

    $after = $content.Length
    Write-Host "  $unitVar pattern: before=$before after=$after changed=$($before -ne $after)"
    return $content
}

# -----------------------------------------------------------------------
# UnitDestructionTests.cs
# -----------------------------------------------------------------------
$file = "C:/Git/Warcrap/UnityRTS/RTS/Assets/Tests/PlayMode/UnitDestructionTests.cs"
$content = [System.IO.File]::ReadAllText($file)

# Use regex for each variable name that appears as a death trigger
# pawn.Update(), building.Update(), warrior.Update(), unit.Update(), target.Update()
# BUT NOT attacker.Update() (that is the IDLE-detection tick, not a death trigger)
foreach ($var in @("pawn", "building", "warrior", "target")) {
    $pattern = "(\t+$var\.Health\s*=\s*[^;]+;\r\n\t+)$var\.Update\(\);"
    $replacement = "`${1}$var.FixedUpdate();"
    $content = [regex]::Replace($content, $pattern, $replacement)
}
# unit.Update() inside the stress-test loop (Health = 0 then unit.Update())
$pattern = "(\t+unit\.Health\s*=\s*[^;]+;\r\n\t+)unit\.Update\(\);"
$replacement = "`${1}unit.FixedUpdate();"
$content = [regex]::Replace($content, $pattern, $replacement)

$remaining = ([regex]::Matches($content, '\.Update\(\)')).Count
Write-Host "UnitDestructionTests.cs remaining .Update() calls: $remaining (expect 2: comment + attacker)"
[System.IO.File]::WriteAllText($file, $content)

# -----------------------------------------------------------------------
# MapReclaimTests.cs
# -----------------------------------------------------------------------
$file = "C:/Git/Warcrap/UnityRTS/RTS/Assets/Tests/PlayMode/MapReclaimTests.cs"
$content = [System.IO.File]::ReadAllText($file)

foreach ($var in @("building", "pawn")) {
    $pattern = "(\t+$var\.Health\s*=\s*[^;]+;\r\n\t+)$var\.Update\(\);"
    $replacement = "`${1}$var.FixedUpdate();"
    $content = [regex]::Replace($content, $pattern, $replacement)
}

$remaining = ([regex]::Matches($content, '\.Update\(\)')).Count
Write-Host "MapReclaimTests.cs remaining .Update() calls: $remaining (expect 0)"
[System.IO.File]::WriteAllText($file, $content)

# -----------------------------------------------------------------------
# UnitManagerQueryTests.cs
# -----------------------------------------------------------------------
$file = "C:/Git/Warcrap/UnityRTS/RTS/Assets/Tests/PlayMode/UnitManagerQueryTests.cs"
$content = [System.IO.File]::ReadAllText($file)

foreach ($var in @("pawn", "w1")) {
    $pattern = "(\t+$var\.Health\s*=\s*[^;]+;\r\n\t+)$var\.Update\(\);"
    $replacement = "`${1}$var.FixedUpdate();"
    $content = [regex]::Replace($content, $pattern, $replacement)
}

$remaining = ([regex]::Matches($content, '\.Update\(\)')).Count
Write-Host "UnitManagerQueryTests.cs remaining .Update() calls: $remaining (expect 0)"
[System.IO.File]::WriteAllText($file, $content)

# -----------------------------------------------------------------------
# UnitCoverageGapTests.cs
# -----------------------------------------------------------------------
$file = "C:/Git/Warcrap/UnityRTS/RTS/Assets/Tests/PlayMode/UnitCoverageGapTests.cs"
$content = [System.IO.File]::ReadAllText($file)

# Death triggers: baseUnit, mine, pawn (in Death_WhileBuilding test)
# Keep: pawn.Update() in IDLE cleanup test and building.Update() in pulse animation test
foreach ($var in @("baseUnit", "mine")) {
    $pattern = "(\t+$var\.Health\s*=\s*[^;]+;\r\n\t+)$var\.Update\(\);"
    $replacement = "`${1}$var.FixedUpdate();"
    $content = [regex]::Replace($content, $pattern, $replacement)
}
# pawn death trigger in Death_WhileBuilding_CleansUpActiveBuilders
$pattern = "(\t+pawn\.Health\s*=\s*[^;]+;\r\n\t+)pawn\.Update\(\);"
$replacement = "`${1}pawn.FixedUpdate();"
$content = [regex]::Replace($content, $pattern, $replacement)

$remaining = ([regex]::Matches($content, '\.Update\(\)')).Count
Write-Host "UnitCoverageGapTests.cs remaining .Update() calls: $remaining (expect 2: IDLE cleanup + pulse loop)"
[System.IO.File]::WriteAllText($file, $content)

Write-Host "All done."
