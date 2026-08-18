[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [ConsoleColor]$Color = [ConsoleColor]::Cyan
    )

    Write-Host ""
    Write-Host "================================" -ForegroundColor $Color
    Write-Host $Text -ForegroundColor $Color
    Write-Host "================================" -ForegroundColor $Color
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

    Write-Section -Text "Linking Ceres skills into $DisplayName"
    Write-Host "Source:      $SourceDir" -ForegroundColor Gray
    Write-Host "Destination: $DestinationDir" -ForegroundColor Gray

    if (-not (Test-Path -LiteralPath $DestinationDir)) {
        New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
        Write-Host "Created destination directory: $DestinationDir" -ForegroundColor Green
    } else {
        $destinationItem = Get-Item -LiteralPath $DestinationDir -Force
        if ($destinationItem.LinkType) {
            Write-Host "Destination is a $($destinationItem.LinkType). Replacing it with a real directory." -ForegroundColor Yellow
            Remove-Item -LiteralPath $DestinationDir -Force
            New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
        }
    }

    $skillDirectories = @(Get-ChildItem -LiteralPath $SourceDir -Directory)
    if ($skillDirectories.Count -eq 0) {
        Write-Host "No skill directories found under source. Skipping." -ForegroundColor Yellow
        return
    }

    $linkedCount = 0
    $skippedCount = 0
    foreach ($skill in $skillDirectories) {
        $destinationSkillPath = Join-Path $DestinationDir $skill.Name
        $sourceSkillPath = [System.IO.Path]::GetFullPath($skill.FullName)

        if (Test-Path -LiteralPath $destinationSkillPath) {
            $existingItem = Get-Item -LiteralPath $destinationSkillPath -Force
            $existingTarget = Resolve-LinkTarget -Path $destinationSkillPath

            if ($existingItem.LinkType -and $existingTarget -eq $sourceSkillPath) {
                Write-Host "  - $($skill.Name): already linked, skipping" -ForegroundColor DarkGray
                $skippedCount++
                continue
            }

            if ($existingItem.LinkType) {
                Write-Host "  - $($skill.Name): replacing stale $($existingItem.LinkType)" -ForegroundColor Yellow
                Remove-Item -LiteralPath $destinationSkillPath -Force
            } else {
                Write-Host "  - $($skill.Name): replacing existing directory" -ForegroundColor Yellow
                Remove-Item -LiteralPath $destinationSkillPath -Recurse -Force
            }
        } else {
            Write-Host "  - $($skill.Name): linking" -ForegroundColor Green
        }

        New-Item -ItemType Junction -Path $destinationSkillPath -Target $sourceSkillPath | Out-Null
        $linkedCount++
    }

    Write-Host ""
    Write-Host "Linked $linkedCount skill(s), $skippedCount already up to date." -ForegroundColor Green
    Write-Host "Other skills already present in $DisplayName were left untouched." -ForegroundColor Green
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$skillsSourcePath = Join-Path $repositoryRoot ".agents\skills"
if (-not (Test-Path -LiteralPath $skillsSourcePath -PathType Container)) {
    throw "Source skills directory not found: $skillsSourcePath"
}

$skillDestinations = @(
    @{
        Name = ".cursor\skills"
        Path = Join-Path $repositoryRoot ".cursor\skills"
    },
    @{
        Name = "~\.codex\skills"
        Path = Join-Path $env:USERPROFILE ".codex\skills"
    },
    @{
        Name = "~\.claude\skills"
        Path = Join-Path $env:USERPROFILE ".claude\skills"
    }
)

Write-Section -Text "Ceres Skills Linker"
Write-Host "Repository root: $repositoryRoot" -ForegroundColor Gray
Write-Host "Source skills:   $skillsSourcePath" -ForegroundColor Gray

foreach ($skillDestination in $skillDestinations) {
    New-PerSkillLinks `
        -SourceDir $skillsSourcePath `
        -DestinationDir $skillDestination.Path `
        -DisplayName $skillDestination.Name
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "Ceres skill edits in .agents\skills now take effect immediately in Cursor, Codex, and Claude." -ForegroundColor Green
