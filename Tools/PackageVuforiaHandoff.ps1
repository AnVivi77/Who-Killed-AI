[CmdletBinding()]
param(
    [string]$OutputRoot = "",
    [string]$Stamp = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "Deliverables"
}
if ([string]::IsNullOrWhiteSpace($Stamp)) {
    $Stamp = Get-Date -Format "yyyyMMdd"
}

$manifestPath = Join-Path $projectRoot "content/assets.manifest.json"
$casePath = Join-Path $projectRoot "content/case.json"
$manifest = Get-Content -Raw -Encoding UTF8 $manifestPath | ConvertFrom-Json
$case = Get-Content -Raw -Encoding UTF8 $casePath | ConvertFrom-Json

$vuforiaRoot = Join-Path $OutputRoot "Vuforia_CaseTargets_$Stamp"
$fullRoot = Join-Path $OutputRoot "ExhibitionAssetHandoff_$Stamp"
$vuforiaZip = "$vuforiaRoot.zip"
$fullZip = "$fullRoot.zip"

# These are generated staging paths owned by this script. Cleaning only these
# exact dated outputs makes repeated packaging deterministic without touching
# SourceAssets, UnityProject, or any user-authored file.
if (Test-Path -LiteralPath $vuforiaRoot) { Remove-Item -LiteralPath $vuforiaRoot -Recurse -Force }
if (Test-Path -LiteralPath $fullRoot) { Remove-Item -LiteralPath $fullRoot -Recurse -Force }
if (Test-Path -LiteralPath $vuforiaZip) { Remove-Item -LiteralPath $vuforiaZip -Force }
if (Test-Path -LiteralPath $fullZip) { Remove-Item -LiteralPath $fullZip -Force }

function Ensure-Directory([string]$path) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

function Copy-ManifestAsset($entry, [string]$destinationRoot, [string]$relativeDestination) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.sourcePath)) {
        return $false
    }
    $source = Join-Path $projectRoot ($entry.sourcePath -replace '/', '\\')
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        return $false
    }
    $destination = Join-Path $destinationRoot ($relativeDestination -replace '/', '\\')
    Ensure-Directory (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $source -Destination $destination -Force
    return $true
}

function LeafName([string]$path) {
    return Split-Path ($path -replace '/', '\\') -Leaf
}

function StemName([string]$path) {
    return [System.IO.Path]::GetFileNameWithoutExtension((LeafName $path))
}

function RelativeFromRoot([string]$path, [string]$root) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    $fullRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    return $fullPath.Substring($fullRoot.Length).Replace('\', '/')
}

Ensure-Directory $OutputRoot
Ensure-Directory $vuforiaRoot
Ensure-Directory $fullRoot

$targetRoot = Join-Path $vuforiaRoot "01_TargetImages"
$targetSpecRoot = Join-Path $vuforiaRoot "02_TargetManager"
Ensure-Directory $targetRoot
Ensure-Directory $targetSpecRoot

$targetEntries = @($manifest.assets | Where-Object { $_.kind -eq "targetImage" })
$targetRows = @()
$missingRows = @()
$clueByTargetAssetId = @{}
foreach ($clueData in @($case.clues)) {
    $clueByTargetAssetId[[string]$clueData.targetImageAssetId] = $clueData
}
$clueByTargetAssetId[[string]$case.detective.targetImageAssetId] = [pscustomobject]@{
    id = $case.detective.informationClueId
    suspectId = $case.detective.id
    slotIndex = 2
}

try {
    Add-Type -AssemblyName System.Drawing
} catch {
    Write-Warning "System.Drawing is unavailable; image dimensions will be left blank."
}

foreach ($entry in $targetEntries) {
    $targetFile = LeafName $entry.suggestedPath
    $copied = Copy-ManifestAsset $entry $targetRoot $targetFile
    $source = if ([string]::IsNullOrWhiteSpace([string]$entry.sourcePath)) { "" } else { Join-Path $projectRoot ($entry.sourcePath -replace '/', '\\') }
    $width = ""
    $height = ""
    $pixelFormat = ""
    $bytes = ""
    $validity = "WAITING_FOR_PHYSICAL_WIDTH"

    if ($copied) {
        $targetPath = Join-Path $targetRoot $targetFile
        $info = Get-Item -LiteralPath $targetPath
        $bytes = $info.Length
        if ($null -ne ([System.Management.Automation.PSTypeName]'System.Drawing.Image').Type) {
            try {
                $image = [System.Drawing.Image]::FromFile($targetPath)
                $width = $image.Width
                $height = $image.Height
                $pixelFormat = $image.PixelFormat.ToString()
                $image.Dispose()
            } catch {
                $validity = "IMAGE_READ_FAILED"
            }
        }
        if ($validity -ne "IMAGE_READ_FAILED") {
            $formatOkay = $targetFile -match '\.(png|jpe?g)$'
            $sizeOkay = ([int64]$bytes -le 2359296)
            $widthOkay = ([int]$width -gt 320)
            $alphaOkay = $pixelFormat -notmatch 'Alpha|PArgb'
            if ($formatOkay -and $sizeOkay -and $widthOkay -and $alphaOkay) {
                $validity = "READY_AFTER_PHYSICAL_WIDTH"
            } else {
                $validity = "CHECK_IMAGE_CONSTRAINTS"
            }
        }
    } else {
        $validity = "MISSING_SOURCE"
        $missingRows += [pscustomobject]@{ Id = $entry.id; SourcePath = $entry.sourcePath; RequiredFor = $entry.requiredFor }
    }

    $resolvedClue = if ($clueByTargetAssetId.ContainsKey([string]$entry.id)) {
        $clueByTargetAssetId[[string]$entry.id]
    } else {
        $null
    }
    $targetRows += [pscustomobject]@{
        VuforiaTargetName = (StemName $targetFile)
        ClueId = if ($null -ne $resolvedClue) { $resolvedClue.id } else { $entry.id }
        SuspectId = if ($null -ne $resolvedClue) { $resolvedClue.suspectId } else { "" }
        Slot = if ($null -ne $resolvedClue) { $resolvedClue.slotIndex } elseif ($entry.id -match '_BEHAVIOR$') { 1 } else { 2 }
        UploadFile = "01_TargetImages/$targetFile"
        SourcePath = $entry.sourcePath
        PhysicalWidthMeters = ""
        PrintedWidthCm = ""
        WidthPixels = $width
        HeightPixels = $height
        FileBytes = $bytes
        PixelFormat = $pixelFormat
        LocalValidation = $validity
        RequiredInput = "Measure the actual printed target width; enter meters in Target Manager and case.json"
    }
}

$targetRows | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $targetSpecRoot "CaseTargets_TargetSpec.csv")
$targetRows | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $targetSpecRoot "CaseTargets_TargetSpec.json")

$targetNames = ($targetRows | ForEach-Object { "- $($_.VuforiaTargetName) -> $($_.ClueId)" }) -join "`n"
$targetReadme = @"
# CaseTargets · Vuforia upload package

Generated: $Stamp
Database type: Vuforia Device Database / Image Targets
Database name: CaseTargets
Target count: $($targetRows.Count) (10 main clues + 1 Detective easter-egg clue)

## Upload files

Upload every file in 01_TargetImages/ to the Vuforia Target Manager. The filenames are deliberately normalized to Vuforia-safe names (letters, numbers, underscore). The image bytes are copied from the source assets; no visual crop or re-export was performed for tracking.

## Target name mapping

$targetNames

## Important missing value

PhysicalWidthMeters is intentionally blank. Measure the real printed width of every target and enter that value in meters in Target Manager. Do not use the on-screen 3840x1152 layout size as the physical target size.

## Result to return to the Unity project

After all targets are accepted, download the Device Database in **Unity .unitypackage** format and save it as:

SourceAssets/06_Vuforia/Database/CaseTargets.unitypackage

Also return the target-width table with the measured values. The project still requires the Vuforia Engine Unity package and a license key, which are not included in this image-only handoff.
"@
$targetReadme | Set-Content -Encoding UTF8 (Join-Path $vuforiaRoot "README.md")

$vuforiaSourceFiles = Get-ChildItem -File -Recurse $targetRoot | Sort-Object FullName
$vuforiaHashes = foreach ($file in $vuforiaSourceFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (RelativeFromRoot $file.FullName $vuforiaRoot)
}
$vuforiaHashes | Set-Content -Encoding UTF8 (Join-Path $vuforiaRoot "SHA256SUMS.txt")

# Build a normalized complete handoff. The Vuforia package above stays small and
# contains only uploadable target images; this package contains all production
# media plus unassigned originals for reference.
$fullDirs = @(
    "01_Videos/Intro",
    "01_Videos/SuspectEvents",
    "01_Videos/Finale",
    "02_Suspects",
    "03_Clues/TargetImages",
    "03_Clues/DisplayImages",
    "04_Environment",
    "05_Extras/Originals_NotInManifest",
    "06_Config",
    "07_Documentation"
)
foreach ($dir in $fullDirs) { Ensure-Directory (Join-Path $fullRoot $dir) }

$fullRows = @()
foreach ($entry in @($manifest.assets | Where-Object { $_.kind -ne "database" })) {
    $dest = $null
    $fileName = LeafName $entry.suggestedPath
    switch ($entry.id) {
        "video_opening" { $dest = "01_Videos/Intro/opening.mp4" }
        "video_background" { $dest = "04_Environment/background.mp4" }
        "video_finale_normal" { $dest = "01_Videos/Finale/normalending.mp4" }
        "video_finale_bad" { $dest = "01_Videos/Finale/badending.mp4" }
        "video_detective_egg" { $dest = "01_Videos/Finale/detective-egg.mp4" }
        "video_s01" { $dest = "01_Videos/SuspectEvents/S01.mp4" }
        "video_s02" { $dest = "01_Videos/SuspectEvents/S02.mp4" }
        "video_s03" { $dest = "01_Videos/SuspectEvents/S03.mp4" }
        "video_s04" { $dest = "01_Videos/SuspectEvents/S04.mp4" }
        "video_s05" { $dest = "01_Videos/SuspectEvents/S05.mp4" }
        "portrait_s01" { $dest = "02_Suspects/S01/portrait.jpeg" }
        "portrait_s02" { $dest = "02_Suspects/S02/portrait.png" }
        "portrait_s03" { $dest = "02_Suspects/S03/portrait.jpeg" }
        "portrait_s04" { $dest = "02_Suspects/S04/portrait.png" }
        "portrait_s05" { $dest = "02_Suspects/S05/portrait.png" }
        "dossier_s01" { $dest = "02_Suspects/S01/dossier.png" }
        "dossier_s02" { $dest = "02_Suspects/S02/dossier.png" }
        "dossier_s03" { $dest = "02_Suspects/S03/dossier.png" }
        "dossier_s04" { $dest = "02_Suspects/S04/dossier.png" }
        "dossier_s05" { $dest = "02_Suspects/S05/dossier.png" }
        default {
            if ($entry.kind -eq "targetImage") {
                $dest = "03_Clues/TargetImages/$fileName"
            }
        }
    }
    if ($null -eq $dest) { continue }
    $copied = Copy-ManifestAsset $entry $fullRoot $dest
    if ($entry.kind -eq "targetImage" -and $copied) {
        [void](Copy-ManifestAsset $entry $fullRoot ("03_Clues/DisplayImages/$fileName"))
    }
    $fullRows += [pscustomobject]@{
        Id = $entry.id
        Kind = $entry.kind
        Destination = $dest
        SourcePath = $entry.sourcePath
        Status = if ($copied) { "copied" } else { "missing" }
        RequiredFor = $entry.requiredFor
    }
}

$firstSourceEntry = @($manifest.assets | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.sourcePath) } | Select-Object -First 1)
$firstSourcePath = Join-Path $projectRoot ($firstSourceEntry.sourcePath -replace '/', '\\')
# Derive the inbox folder from a real manifest path so this script remains
# Unicode-safe when launched by Windows PowerShell 5.1.
$inboxRoot = Split-Path (Split-Path $firstSourcePath -Parent) -Parent
$extraFiles = @()
foreach ($candidate in @(Get-ChildItem -File -Recurse $inboxRoot)) {
    if ($candidate.Extension -notmatch '\.(png|jpe?g|mp4|mov|wav|mp3)$') { continue }
    $candidateRelative = RelativeFromRoot $candidate.FullName $projectRoot
    $isManifestSource = $false
    foreach ($manifestAsset in @($manifest.assets)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$manifestAsset.sourcePath) -and
            [string]::Equals($manifestAsset.sourcePath.Replace('\', '/'), $candidateRelative, [System.StringComparison]::OrdinalIgnoreCase)) {
            $isManifestSource = $true
            break
        }
    }
    if (-not $isManifestSource) { $extraFiles += $candidate }
}
Write-Verbose ("Project root: " + $projectRoot)
Write-Verbose ("Extra original count: " + @($extraFiles).Count)
foreach ($extra in $extraFiles) {
    $extraRel = RelativeFromRoot $extra.FullName $inboxRoot
    $extraDest = Join-Path "05_Extras/Originals_NotInManifest" $extraRel
    Ensure-Directory (Join-Path $fullRoot (Split-Path -Parent $extraDest))
    Copy-Item -LiteralPath $extra.FullName -Destination (Join-Path $fullRoot $extraDest) -Force
    $fullRows += [pscustomobject]@{
        Id = ""
        Kind = "extraOriginal"
        Destination = $extraDest.Replace('\', '/')
        SourcePath = (RelativeFromRoot $extra.FullName $projectRoot)
        Status = "copied_for_reference"
        RequiredFor = "not_bound_by_case_manifest"
    }
}

Copy-Item -LiteralPath $casePath -Destination (Join-Path $fullRoot "06_Config/case.json") -Force
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $fullRoot "06_Config/assets.manifest.json") -Force
$handoffDocPath = Get-ChildItem -File (Join-Path $projectRoot "docs") | Where-Object { $_.Name -like "12_*" } | Select-Object -First 1 -ExpandProperty FullName
Write-Verbose ("Copying delivery documentation: " + $handoffDocPath)
Ensure-Directory (Join-Path $fullRoot "07_Documentation")
    Copy-Item -LiteralPath $handoffDocPath -Destination (Join-Path $fullRoot "07_Documentation/12_Vuforia_Handoff_and_Maintenance.md") -Force

$fullRows | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $fullRoot "06_Config/NormalizedAssetInventory.csv")
$fullRows | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $fullRoot "06_Config/NormalizedAssetInventory.json")

$fullReadme = @"
# Exhibition asset handoff · $Stamp

This archive is the normalized handoff for the Unity + Vuforia exhibition. It contains the production videos, five dossiers, portraits, eleven clue images, configuration sources, and extra original images that were present in the inbox but are not referenced by assets.manifest.json.

The Vuforia Device Database is **not** inside this archive because it must be generated from the images in the Vuforia Target Manager after the real printed widths are known. Use the separate Vuforia_CaseTargets_$Stamp package for that upload step.

See 07_Documentation/12_Vuforia_Handoff_and_Maintenance.md and 06_Config/NormalizedAssetInventory.csv for the mapping.
"@
$fullReadme | Set-Content -Encoding UTF8 (Join-Path $fullRoot "README.md")

$fullMedia = Get-ChildItem -File -Recurse $fullRoot | Where-Object { $_.FullName -notmatch '\\06_Config\\|\\07_Documentation\\|README\.md$' } | Sort-Object FullName
$fullHashes = foreach ($file in $fullMedia) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (RelativeFromRoot $file.FullName $fullRoot)
}
$fullHashes | Set-Content -Encoding UTF8 (Join-Path $fullRoot "SHA256SUMS.txt")

$summary = [pscustomobject]@{
    Generated = $Stamp
    ProjectRoot = $projectRoot
    UnityVersion = "2022.3.62f3c1"
    CaseId = $case.caseId
    SuspectCount = @($case.suspects).Count
    MainClueCount = @($case.clues).Count
    TargetImageCount = $targetRows.Count
    VuforiaDatabaseStatus = "not_generated_requires_target_manager_account_widths_and_download"
    MissingManifestSources = $missingRows.Count
    VuforiaPackage = $vuforiaZip
    CompleteHandoffPackage = $fullZip
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $OutputRoot "PackageSummary_$Stamp.json")

Compress-Archive -Path (Join-Path $vuforiaRoot '*') -DestinationPath $vuforiaZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $fullRoot '*') -DestinationPath $fullZip -CompressionLevel Optimal -Force

Write-Output "Vuforia package: $vuforiaZip"
Write-Output "Complete handoff: $fullZip"
Write-Output "Target images: $($targetRows.Count)"
Write-Output "Missing manifest sources: $($missingRows.Count)"
