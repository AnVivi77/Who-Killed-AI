# CaseTargets 路 Vuforia upload package

Generated: 20260914
Database type: Vuforia Device Database / Image Targets
Database name: CaseTargets
Target count: 11 (10 main clues + 1 Detective easter-egg clue)

## Upload files

Upload every file in 01_TargetImages/ to the Vuforia Target Manager. The filenames are deliberately normalized to Vuforia-safe names (letters, numbers, underscore). The image bytes are copied from the source assets; no visual crop or re-export was performed for tracking.

## Target name mapping

- Clue_S01_Behavior -> CL_S01_BEHAVIOR
- Clue_S01_Information -> CL_S01_INFORMATION
- Clue_S02_Behavior -> CL_S02_BEHAVIOR
- Clue_S02_Information -> CL_S02_INFORMATION
- Clue_S03_Behavior -> CL_S03_BEHAVIOR
- Clue_S03_Information -> CL_S03_INFORMATION
- Clue_S04_Behavior -> CL_S04_BEHAVIOR
- Clue_S04_Information -> CL_S04_INFORMATION
- Clue_S05_Behavior -> CL_S05_BEHAVIOR
- Clue_S05_Information -> CL_S05_INFORMATION
- Detective_Information -> CL_DETECTIVE_INFORMATION

## Important missing value

PhysicalWidthMeters is intentionally blank. Measure the real printed width of every target and enter that value in meters in Target Manager. Do not use the on-screen 3840x1152 layout size as the physical target size.

## Result to return to the Unity project

After all targets are accepted, download the Device Database in **Unity .unitypackage** format and save it as:

SourceAssets/06_Vuforia/Database/CaseTargets.unitypackage

Also return the target-width table with the measured values. The project still requires the Vuforia Engine Unity package and a license key, which are not included in this image-only handoff.
