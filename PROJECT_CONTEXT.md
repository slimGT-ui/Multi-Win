# Multi-Win — Project Context & Development Handoff

> **Purpose:** This file is the durable working context for future ChatGPT sessions and contributors. Read it before making changes. Keep it updated whenever project status, decisions, milestones, or next steps change.
>
> **Last updated:** 2026-11-15  
> **Repository:** https://github.com/slimGT-ui/Multi-Win  
> **Default branch:** `main`  
> **Project owner:** `slimGT-ui`

---

## 1. Project identity

**Multi-Win** is a WPF desktop application targeting .NET 8 for preparing bootable Windows and Linux installation media.

The project includes a built-in OS catalog, custom/local ISO support, Windows setup customization, USB diagnostics, and a Windows Setup mode that can boot from an internal drive without a USB stick.

The project is in **beta / pre-validation**. The published beta must be tested before we treat the current feature set as reliable or move toward a stable release.

### Technology

- C# / .NET 8
- WPF
- Windows desktop application
- Project file: `MultiWin.csproj`
- Main UI: `MainWindow.xaml` and `MainWindow.xaml.cs`
- Catalog: `Data/catalog.json`
- Business logic is separated into `Services/`; data models are in `Models/`.

---

## 2. Current state — read this first

### Release and validation state

- Latest published release known to this project context: **`v0.6.0-beta.1`**.
- The user explicitly said this beta has **not yet been tested**.
- Do not describe the beta as tested, stable, or verified.
- The user has not started the planned beta validation yet. They said they will test it and return with results.
- The current development priority is to validate the existing baseline and fix discovered issues, not to add a large number of new features immediately.

### Latest merged change

**PR #4 — Simplify Windows catalog to four choices**

- PR: https://github.com/slimGT-ui/Multi-Win/pull/4
- Branch: `feature/simplified-windows-catalog`
- Merge commit: `2e88bda98845bbbab8cccac686ee32a307912b13`
- Status: merged into `main`.
- Catalog now exposes four choices:
  1. Windows 11 (25H2 x64)
  2. Windows 11 Pro (25H2 x64)
  3. Windows 10 (22H2 x64)
  4. Windows 10 Pro (22H2 x64)
- Each pair (Home/default and Pro) shares the same ISO URL for its Windows version.
- `WindowsImage` gained optional `EditionId`.
- Pro catalog entries use `EditionId: "Professional"`.
- `BypassService` applies edition selection through `sources/ei.cfg` and refuses to overwrite a conflicting existing file.
- Both USB preparation and internal-drive Setup paths pass the selected edition through.
- README catalog documentation was updated.

**Important limitations of PR #4:**

- The change was merged, but no .NET build, automated test, boot test, or Windows Setup installation test was recorded as completed.
- The user clarified that this catalog cleanup was done proactively to remove unnecessary Windows choices; it does **not** mean they have started testing the beta.
- The published `v0.6.0-beta.1` release predates PR #4. A new build/package is needed to test the four-choice catalog and Pro selection.
- Pro edition selection must be checked in the actual Windows Setup flow for both Windows 10 and Windows 11.
- Catalog ISO hashes are currently blank.
- Current catalog download URLs are third-party Archive.org mirrors, not verified official Microsoft links. Do not present them as official or authenticated.
- No end-to-end success claim is justified until the relevant tests have actually passed.

### Immediate next action

**Wait for the user to test the beta and report what happened.**

When the user returns:
1. Ask for / inspect the exact failure, log, screenshot, or reproduction steps if needed.
2. Reproduce or inspect the relevant code before changing it.
3. Fix the issue in a focused branch.
4. Build and run available automated checks where possible.
5. Clearly distinguish code-level checks from real hardware / Windows Setup validation.
6. Update this file with the result and next action.

Do not start unrelated feature work while the user is carrying out the baseline test unless the user explicitly asks.

---

## 3. Product capabilities currently documented

The README describes the following functionality. Treat this as the documented feature set, **not proof that every path has passed end-to-end testing**.

- Built-in Windows catalog with Windows 10, Windows 10 Pro, Windows 11, Windows 11 Pro.
- Local ISO selection, drag-and-drop ISO input, and direct ISO URL.
- Content-aware ISO detection using image contents / metadata.
- Windows installation media preparation by copying files.
- Linux ISO raw writing.
- FAT32 mode with automatic splitting of oversized `install.wim`.
- Handling / conversion path for large `install.esd` images.
- NTFS direct-copy mode.
- MBR and GPT partition layout choices.
- Windows 11 hardware requirement bypass options (TPM, Secure Boot, RAM, CPU).
- Optional Windows Setup tweaks: skip online Microsoft account, create local user, skip privacy questions, prevent automatic BitLocker device encryption, Appraiser compatibility tweak, use current PC time zone.
- Hardware compatibility information and warnings.
- Optional USB bad-block test and post-write verification.
- USB sequential read/write benchmark.
- Windows Setup from an internal drive using a `MULTIWIN-SETUP` folder and one-time boot entry; normal default boot entry is intended to remain unchanged.
- Progress, transfer speed, ETA, operation log, log export.
- ISO cache manager under `%TEMP%\\Multi-Win`.

USB writing and internal-drive boot setup are high-impact operations. Treat them as safety-critical.

---

## 4. Development rules and collaboration agreement

These rules reflect the user's explicit preferences. Follow them in every future session.

### 4.1 Preserve context

- Read this file at the beginning of every new development session.
- Before substantial work, inspect the current repository state, recent commits, open PRs, and relevant source files. This document is a handoff aid, not a substitute for checking GitHub.
- Do not assume a task is still pending just because an old note says so. Verify current repository state.
- When a decision changes, update this file in the same workstream.
- Keep completed work, pending work, known bugs, and unverified assumptions separate.

### 4.2 Git branches, commits, and pull requests

- Use focused branches with descriptive names, for example `fix/iso-validation`, `feature/download-integrity`, `docs/project-context`.
- Avoid creating many tiny branches for changes that belong to one coherent task.
- Branch / PR descriptions should state:
  - problem and motivation;
  - scope and intended behavior;
  - key files / components affected;
  - implementation decisions;
  - tests actually run and their results;
  - known limitations and manual checks still required.
- Commit messages must be informative. Prefer a concise subject plus a detailed body when the change is substantial.
- Explain **what changed, why it changed, important implementation details, and what was / was not verified**.
- Never write that a test passed unless it was actually run and its result observed.
- Do not claim a branch was pushed, a PR opened, a PR merged, or a release created unless the corresponding GitHub action returned success and the result was checked.
- Do not merge automatically just because implementation is complete. Review the diff and checks first; follow the user's established workflow and explicit direction.

### 4.3 Testing and status labels

Use these terms consistently:

- **Planned** — not implemented.
- **Implemented** — code has been written; this alone does not imply it builds or works.
- **Build-checked** — a build command completed successfully.
- **Unit-tested** — relevant automated tests ran and passed.
- **Manually tested** — a person exercised the behavior and reported the result.
- **Hardware-verified** — tested on actual target hardware / boot environment.
- **Release-verified** — the packaged release artifact itself was tested.

A feature may have more than one status. Do not promote a feature to a stronger status without evidence.

### 4.4 Safety requirements

- Treat disk selection, partitioning, formatting, USB writing, and boot configuration as destructive / high-risk operations.
- Preserve explicit user confirmation before destructive operations.
- Validate the selected target carefully; never silently guess a target disk.
- Handle cancellation and failure without leaving disks or boot configuration in an unsafe partial state.
- Test with a spare USB drive and non-critical hardware first.
- Warn clearly that USB creation erases the selected drive.
- Do not test internal-drive installation on a machine containing important data without a verified backup.
- Prefer defensive checks and clear error messages over silent recovery that could hide a dangerous state.

### 4.5 Implementation quality

- Keep changes focused and consistent with the existing architecture.
- Inspect existing code and call paths before editing; do not guess method names, model fields, or service behavior.
- Avoid unrelated refactors during bug fixes.
- Add tests for logic that can be tested without destructive hardware operations.
- Review error handling, cancellation, cleanup, logging, and resource disposal in long-running operations.
- Keep user-facing messages understandable and actionable.
- Update README / changelog / this context file when behavior or project state changes.

---

## 5. Proposed development roadmap

This roadmap captures the priorities discussed so far. It is a **working plan**, not a claim that every phase has been formally approved or completed. Reorder it when test results or the user's instructions justify doing so.

### Phase 0 — Validate the existing beta baseline

**Status: Planned / waiting for user testing**

Goal: establish what the already-published `v0.6.0-beta.1` actually does on the user's machine before making assumptions about bugs.

Suggested checks:
- Launch and basic UI behavior.
- Download an image from the built-in catalog.
- Select and process a local ISO.
- Use a direct ISO URL.
- Inspect logs and error messages.
- Check basic device detection and operation cancellation where safe.

Record the exact environment, steps, result, and logs for every failure. Do not use a valuable USB drive.

Note: this published beta does not contain the later PR #4 catalog cleanup.

### Phase 1 — Stabilize critical workflows

**Status: Planned**

Prioritize bugs found during Phase 0, especially:
- incorrect or unsafe target-drive selection;
- incomplete cleanup after failure or cancellation;
- download interruptions and invalid / incomplete downloads;
- insufficient free-space checks;
- ISO parsing / structure errors;
- misleading progress or success reporting;
- boot configuration rollback and recovery.

Only implement issues supported by code inspection or reproducible evidence.

### Phase 2 — ISO integrity and validation

**Status: Planned**

- Add SHA-256 validation for catalog images when trusted expected hashes are available.
- Populate catalog hashes only from a trustworthy source and verify them independently.
- Validate downloaded file size and completion before treating a download as ready.
- Validate ISO structure and required Windows / Linux files before writing.
- Clearly distinguish checksum unavailable from checksum mismatch.
- Do not invent or copy unverified checksums.

### Phase 3 — Verify Windows catalog and edition selection

**Status: Planned; PR #4 implementation merged but unverified**

- Build the current `main` after PR #4.
- Confirm exactly four catalog entries appear.
- Verify Windows 10 and Windows 11 image selection.
- Verify Pro selection is carried through USB creation and internal-drive Setup.
- Boot the resulting media and confirm the correct edition is offered / selected by Windows Setup.
- Verify behavior when `sources/ei.cfg` already exists or conflicts.
- Reassess catalog mirror availability and trustworthiness.

### Phase 4 — Automated tests and diagnostics

**Status: Planned**

Add tests around non-destructive logic, prioritizing:
- catalog parsing and validation;
- edition-selection / `ei.cfg` behavior;
- ISO classification and structural validation;
- download validation;
- bypass / unattended-setup file generation;
- path handling and cleanup logic.

Also improve diagnostics where real test failures show missing information. Keep hardware-dependent tests separate from unit tests.

### Phase 5 — UX and operational clarity

**Status: Planned; prioritize based on user testing**

Potential improvements:
- clearer target-drive identification (model, capacity, removable status);
- more useful download / write progress, speed, and ETA;
- clearer ISO metadata and detected image details;
- clearer summaries of selected Windows edition and setup tweaks;
- settings persistence where appropriate;
- better recovery guidance and log export.

Avoid adding cosmetic features while critical safety or correctness bugs remain.

### Phase 6 — Release candidate and release validation

**Status: Not started**

Before a new beta / stable release:
- build from a clean checkout;
- run automated tests;
- publish a fresh package containing all merged changes;
- test the actual release artifact, not only a local debug build;
- validate USB boot in UEFI and, where supported, Legacy BIOS;
- validate Windows Setup and edition selection;
- validate Linux raw-write flow;
- validate cancellation / failure behavior;
- document remaining limitations and known issues;
- update README and changelog;
- only then decide whether the build is ready for a release candidate or stable release.

### Future feature ideas — backlog, not current priorities

These were discussed as possible future directions and must not displace stabilization work:
- Disk Health view (SMART / temperature, where supported).
- Multi-ISO Manager.
- Recovery Toolkit.
- Installation Profiles / reusable presets.

---

## 6. Test matrix to maintain

Update this table as evidence arrives. Use exact build / commit / device details where possible.

| Area | Status | Evidence / notes |
|---|---|---|
| Launch and basic UI | Not tested in this handoff | User has not yet reported beta test results |
| Built-in catalog download | Not tested | Test on a fresh current build |
| Local ISO | Not tested | |
| Direct ISO URL | Not tested | |
| Windows 10 USB | Not tested | Spare USB only |
| Windows 11 USB | Not tested | Spare USB only |
| Windows 10 Pro selection | Not tested | PR #4; verify in Windows Setup |
| Windows 11 Pro selection | Not tested | PR #4; verify in Windows Setup |
| FAT32 / WIM splitting | Not tested in this handoff | |
| NTFS mode | Not tested in this handoff | |
| MBR / GPT boot | Not tested in this handoff | |
| Linux raw image | Not tested in this handoff | |
| Post-write verification | Not tested in this handoff | |
| Cancellation / interrupted operation | Not tested in this handoff | |
| Internal-drive Setup | Not tested in this handoff | High-risk; backup and spare/test environment |
| Release package | Not tested in this handoff | Published beta predates PR #4 |

---

## 7. Known risks and open questions

1. **Published artifact is behind source:** `v0.6.0-beta.1` predates the merged four-choice catalog change.
2. **No baseline test report yet:** the user has not tested the published beta.
3. **No build verification recorded for PR #4:** compile status is unknown from this handoff.
4. **Pro edition behavior is unverified:** `ei.cfg` handling must be validated in Windows Setup for both OS versions.
5. **ISO provenance:** current catalog uses third-party Archive.org mirrors. Authenticity and availability are not guaranteed.
6. **No trusted SHA-256 values recorded:** do not enable strict hash checks with guessed values.
7. **Destructive workflows:** USB writing and internal-drive setup require careful manual testing and explicit safety checks.

---

## 8. Session handoff checklist

At the start of a new ChatGPT session:

1. Read this file completely.
2. Check the repository's current default branch and latest commits.
3. Check open PRs and recently merged PRs.
4. Check whether the immediate next action in Section 2 has changed.
5. Confirm with the user only if an essential decision or test result is missing; do not make them repeat information already recorded here.
6. Work on one coherent task at a time.
7. Before ending a work session, update:
   - current phase and status;
   - exact branch / PR / commit references;
   - files changed;
   - commands and tests actually run, with results;
   - manual checks still needed;
   - known issues and the single clearest next action.

### Copy/paste prompt for a new chat

> We are continuing development of slimGT-ui/Multi-Win. First read PROJECT_CONTEXT.md from the repository that I attached / linked. Treat it as the project handoff document. Verify its status against the current GitHub repository, recent commits, and PRs before acting. Preserve the development rules, do not claim unrun tests passed, update PROJECT_CONTEXT.md whenever project status or decisions change, and continue from the documented immediate next action. Do not restart planning from scratch.

---

## 9. Change log for this context document

- **2026-11-15:** Initial handoff document created. Recorded the .NET 8 WPF project identity, user's development / Git documentation preferences, PR #4 catalog cleanup, known verification gaps, the untested `v0.6.0-beta.1` baseline, and a phased stabilization-to-release roadmap.
