---
title: "slinger-character"
description: "Implement Slinger character, animation controller, prefab, and spawning integration"
status: completed
priority: P1
effort: "2h"
tags: ["gameplay", "character", "unity", "spawning"]
created: 2026-09-05
---

# Slinger Character Implementation Plan

## Overview
Implement the **Slinger** character as a mid-range skirmisher holding a slingshot, complete with dedicated animation controller support, stats, unit prefab, and spawning integration through the existing `SlingShot` button on the gameplay HUD.

## Goals

| # | Goal | Priority |
|---|------|----------|
| 1 | Configure Slinger Animator Controller with `isAttack` bool parameter and hook `AttackAnimationFinished` event | P1 |
| 2 | Create `Slinger.cs` component with skirmisher stats (Range: 4.5, Damage: 8, HP: 25, Cost: 15, Speed: 3.5) | P1 |
| 3 | Create `Slinger.prefab` and integrate with `GameManager.cs` and `Gameplay.unity` scene | P1 |
| 4 | Verify clean build (`dotnet build`) and runtime spawning pipeline | P1 |

## Phases

| # | Phase | Status |
|---|-------|--------|
| 1 | [Phase 1: Animation & Animator Controller Setup](./phase-01-start.md) | Completed |
| 2 | [Phase 2: Slinger Component Script](./phase-02-component-script.md) | Completed |
| 3 | [Phase 3: Slinger Prefab & GameManager Integration](./phase-03-prefab-gamemanager.md) | Completed |
| 4 | [Phase 4: Build Verification & Playtest Validation](./phase-04-verification.md) | Completed |

## Success Criteria

- [x] Slinger attack animation triggers `AttackAnimationFinished` callback to inflict damage on enemy units
- [x] `Slinger.cs` handles mid-range engagement (4.5m) and stops behind friendly frontline troops
- [x] `Slinger.prefab` exists with proper SpriteRenderer, Collider2D, Rigidbody2D, Animator, and `Slinger` script
- [x] Clicking the SlingShot button in `Gameplay.unity` spends 15 gold and spawns a Slinger at Blue Gate moving toward Red Gate
- [x] `dotnet build Fortress-Clash-.sln` builds with 0 errors

<!-- slug: slinger-character -->