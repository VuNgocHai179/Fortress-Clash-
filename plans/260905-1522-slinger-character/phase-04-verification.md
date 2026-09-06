---
phase: 4
title: "Build Verification & Playtest Validation"
status: pending
priority: P1
effort: "15m"
dependencies: [1, 2, 3]
---

# Phase 4: Build Verification & Playtest Validation

## Overview
Perform static analysis, full C# solution compilation (`dotnet build`), asset metadata validation, and runtime readiness checks to ensure the Slinger character operates smoothly without warnings or missing dependencies.

## Requirements
- Functional:
  - Solution compiles with 0 errors and 0 warnings.
  - All new assets (`Slinger.cs`, `slingshotanimator.controller`, `Slinger.prefab`) have valid matching `.meta` files.
  - Scene links are consistent.
- Non-functional:
  - Clean git diff with no corrupted binary or YAML files.

## Related Code Files
- Inspect: `Assembly-CSharp.csproj`
- Inspect: `Assets/Scenes/Gameplay.unity`
- Inspect: `Fortress-Clash-.sln`

## Implementation Steps
1. Run `dotnet build Fortress-Clash-.sln` and check exit code.
2. Verify all new assets have corresponding `.meta` files with distinct GUIDs.
3. Validate that `Gameplay.unity` contains the assigned `Slinger` property on `GameManager` and the `SlingShot` button event points to `GameManager.SlingshotChoosen`.
4. Document playtesting steps for the user to try in Unity Editor.

## Success Criteria
- [x] `dotnet build Fortress-Clash-.sln` exits with code 0 (0 errors, 0 warnings)
- [x] All `.meta` files exist and are valid YAML
- [x] All scene references verified

## Risk Assessment
- *Risk:* Unity Editor requires asset database refresh for new files.
- *Mitigation:* Document that Unity Editor will automatically reimport new assets on focus.
