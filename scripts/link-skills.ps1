[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Select-LinkDestinations {
    param([Parameter(Mandatory = $true)][object[]]$Destinations)

    $activeIndex = 0
    $selected = @($Destinations | ForEach-Object { $false })
    $lineWidth = [Console]::WindowWidth - 1
    $cursorVisible = [Console]::CursorVisible
    [Console]::CursorVisible = $false
    try {
        foreach ($destination in $Destinations) {
            [Console]::WriteLine()
        }
        $listTop = [Console]::CursorTop - $Destinations.Count

        while ($true) {
            for ($i = 0; $i -lt $Destinations.Count; $i++) {
                [Console]::SetCursorPosition(0, $listTop + $i)
                $pointer = if ($i -eq $activeIndex) { ">" } else { " " }
                $mark = if ($selected[$i]) { "[x]" } else { "[ ]" }
                $line = "$pointer $mark $($Destinations[$i].Name)"
                [Console]::Write($line.PadRight($lineWidth))
            }

            switch ([Console]::ReadKey($true).Key) {
                "UpArrow" { $activeIndex = ($activeIndex - 1 + $Destinations.Count) % $Destinations.Count }
                "DownArrow" { $activeIndex = ($activeIndex + 1) % $Destinations.Count }
                "Spacebar" { $selected[$activeIndex] = -not $selected[$activeIndex] }
                "Enter" {
                    $result = @(for ($i = 0; $i -lt $Destinations.Count; $i++) {
                        if ($selected[$i]) { $Destinations[$i] }
                    })
                    if ($result.Count -gt 0) {
                        [Console]::SetCursorPosition(0, $listTop + $Destinations.Count)
                        return $result
                    }
                }
            }
        }
    } finally {
        [Console]::CursorVisible = $cursorVisible
    }
}

function Resolve-LinkTarget {
    param([Parameter(Mandatory = $true)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.LinkType) {
        return $null
    }

    $target = $item.Target
    if ($target -is [System.Array]) {
        $target = $target[0]
    }

    if ([string]::IsNullOrWhiteSpace($target)) {
        return $null
    }

    try {
        return [System.IO.Path]::GetFullPath($target)
    } catch {
        return $target
    }
}

function New-PerSkillLinks {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$DestinationDir,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )

    Write-Host ""
    Write-Host "Linking Ceres skills into $DisplayName" -ForegroundColor Cyan
    Write-Host "Source:      $SourceDir" -ForegroundColor Gray
    Write-Host "Destination: $DestinationDir" -ForegroundColor Gray

    if (-not (Test-Path -LiteralPath $DestinationDir)) {
        New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    }

    $skillDirectories = @(Get-ChildItem -LiteralPath $SourceDir -Directory)
    $linkedCount = 0
    $skippedCount = 0
    foreach ($skill in $skillDirectories) {
        $destinationSkillPath = Join-Path $DestinationDir $skill.Name
        $sourceSkillPath = [System.IO.Path]::GetFullPath($skill.FullName)

        $existingItem = Get-Item -LiteralPath $destinationSkillPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $existingItem) {
            $existingTarget = Resolve-LinkTarget -Path $destinationSkillPath

            if ($existingItem.LinkType -and $existingTarget -eq $sourceSkillPath) {
                Write-Host "  - $($skill.Name): already linked, skipping" -ForegroundColor DarkGray
                $skippedCount++
                continue
            }

            if (-not $existingItem.LinkType) {
                Write-Host "  - $($skill.Name): destination already exists, skipping" -ForegroundColor Yellow
                $skippedCount++
                continue
            }

            Remove-Item -LiteralPath $destinationSkillPath -Force
            Write-Host "  - $($skill.Name): replacing an existing skill link" -ForegroundColor Yellow
        }

        New-Item -ItemType Junction -Path $destinationSkillPath -Target $sourceSkillPath | Out-Null
        Write-Host "  - $($skill.Name): linked" -ForegroundColor Green
        $linkedCount++
    }

    Write-Host "Linked $linkedCount skill(s), skipped $skippedCount." -ForegroundColor Gray
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$skillsSourcePath = Join-Path $repositoryRoot "Plugins\ceres\skills"
if (-not (Test-Path -LiteralPath $skillsSourcePath)) {
    throw "Ceres plugin skills directory not found: $skillsSourcePath"
}
$availableDestinations = @(
    @{
        Name = "Cursor (.cursor\skills)"
        Path = Join-Path $repositoryRoot ".cursor\skills"
    },
    @{
        Name = "Claude (~\.claude\skills)"
        Path = Join-Path $env:USERPROFILE ".claude\skills"
    },
    @{
        Name = "Codex (~\.codex\skills)"
        Path = Join-Path $env:USERPROFILE ".codex\skills"
    }
)

Write-Host "Ceres Skills Linker" -ForegroundColor Cyan
Write-Host "Repository root: $repositoryRoot" -ForegroundColor Gray
Write-Host "Source skills:   $skillsSourcePath" -ForegroundColor Gray

$skillDestinations = @(Select-LinkDestinations -Destinations $availableDestinations)
foreach ($skillDestination in $skillDestinations) {
    New-PerSkillLinks `
        -SourceDir $skillsSourcePath `
        -DestinationDir $skillDestination.Path `
        -DisplayName $skillDestination.Name
}

Write-Host ""
Write-Host "Done. Ceres skill edits now take effect immediately in the selected agents." -ForegroundColor Green
