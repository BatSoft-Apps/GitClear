# GitClear — Design

A Windows desktop tool that reclaims disk space by deleting git-ignored files
from a selected repository.

## Status legend

- **[decided]** — ruled on by the user; do not relitigate (see the "why" inline).
- **[leaning]** — my recommendation, awaiting user sign-off. Not yet binding.
- **[open]** — undecided; see **Open decisions**.

Reading order: this overview → **Open decisions** → **Backlog**. All design
documents live in `Specifications/`; split this file into further documents there
once any one area outgrows a screen.

---

## PROD — Product

**PROD-1** The app follows this flow:

1. User points the app at a root folder.
2. App discovers all git repositories under that folder (**DISC-1**).
3. User selects one repository.
4. App asks git which files that repo ignores, honoring nested `.gitignore`
   overrides (**SCAN-1** — git does the honoring, not us).
5. App builds the set of ignored files and their sizes (**SCAN-2**).
6. App shows a File-Explorer-style three-pane view: repository list, folder tree
   with aggregate ignored size per folder, and a file list with per-file size
   (**UI-1**).
7. User checks folders and/or individual files to clear (**UI-2**).
8. App moves the selected targets to the Recycle Bin, deleting wholly-ignored
   folders as a unit; a single-level Undo can restore them (**DEL-1/4**).

---

## ARCH — Architecture

**ARCH-1** Windows-only desktop application. **[decided]**
*Why:* target audience is Windows; unlocks native tree/list UX and simplest
single-exe distribution.

**ARCH-2** .NET 10 (LTS), C#, **WPF** for the UI. WinUI 3 and MAUI rejected. **[decided]**
*(Target framework `net10.0-windows`; .NET 10 is the installed LTS on the build machine.)*
*Why:* the app is control-heavy (`TreeView` + `ListView` + tri-state checkboxes
+ virtualization for huge trees) and filesystem-heavy (Win32 shell interop for
the Recycle Bin) — WPF's core strengths, with zero deployment friction (plain
`.exe`). WinUI 3 costs Windows App SDK packaging overhead and has rougher
tree/virtualization for a Fluent look this utility does not need. MAUI is a
mobile-first cross-platform framework that on Windows renders via WinUI 3
anyway — wrong altitude for a Windows-only desktop utility, with weak desktop
data controls.

**ARCH-3** The set of ignored files is obtained **from the installed `git`
CLI**, never from a reimplementation of git's ignore logic. **[decided]**
*Why:* git's ignore engine (negation, nested overrides, `info/exclude`, global
excludes, `**` globs) is deep; mismatching it risks deleting a tracked file or
missing junk. `git ls-files` gives the authoritative answer that matches the
user's real workflow. Requires git on PATH — near-certain, since the user is
scanning git repos. Missing-git is a handled error, not a fallback engine.

**ARCH-4** Deletion moves items to the **Windows Recycle Bin**, not permanent
delete. **[decided]**
*Why:* destructive tool; cheap undo insurance against a mistaken selection.

**ARCH-5** Layout: `GitClear.Core` (UI-free domain + services) is separate from
`GitClear.App` (WPF/MVVM). MVVM via CommunityToolkit.Mvvm; DI via
Microsoft.Extensions.DependencyInjection. The app builds a container in
`App.OnStartup`, resolves the main window from it, and installs a
last-resort `DispatcherUnhandledException` handler; Core exposes one
registration entry point, `AddGitClearCore()`. Shared build settings live in
`Directory.Build.props` (nullable, warnings-as-errors). Core logic is tested
against real git/filesystem; the app view models are tested with fakes. The
Recycle-Bin tests deliberately **recycle then restore** a throwaway temp file and
folder — this exercises the real shell interop in both directions while leaving no
residue in the user's Recycle Bin on every test run.
```
GitClear.slnx
├─ src/GitClear.Core/   Discovery/ · Scanning/ · Git/ · Deletion/ · Model/ · DependencyInjection/
├─ src/GitClear.App/    Views/ · ViewModels/ · Services/ · Behaviors/ · Formatting/
├─ tests/GitClear.Core.Tests/   (xUnit, real git + Recycle Bin)
└─ tests/GitClear.App.Tests/    (xUnit, view models via fakes)
```

---

## DISC — Repository discovery

**DISC-1** Given a root folder, find repos by locating directories that contain
a `.git` entry — which may be a **folder** (normal repo) or a **file** (worktree
/ submodule pointer). Discovery runs on a background thread.

**DISC-2** Once a `.git` is found on a path, stop recursing beneath it — do not
hunt for repos nested inside a repo. **[decided]**
*Why:* matches the "pick a repo" mental model; nested content is normally
already ignored by the parent, and true submodules belong to the parent repo.

---

## SCAN — Ignore scan & sizing

**SCAN-1** For the selected repo, obtain ignored entries by running, at the repo
root:
```
git ls-files --others --ignored --exclude-standard -z --directory
```
- `--others --ignored --exclude-standard` = things git ignores.
- `-z` = NUL-terminated output, so odd filenames are safe (parsed from raw
  UTF-8 bytes).
- `--directory` = a **wholly-ignored directory collapses to one entry** ending
  in `/` (verified: `node_modules/`, `src/bin/`, `tools/node_modules/`); files
  in **mixed** folders (which also hold tracked content) are listed
  individually (e.g. `src/debug.log`). git remains the sole authority — we never
  walk the filesystem to decide what is ignored.
- *Why `--directory` (reversing an earlier "no `--directory`" call):* a
  collapsed directory is guaranteed by git to contain no tracked files, which is
  exactly the proof needed to delete it **wholesale** — safe *and* far faster
  than recycling its files one by one (DEL-1). Deleting a folder full of
  thousands of files was the main performance complaint.

**SCAN-2** Sizes: `stat` each individual file (`FileInfo.Length`); **walk** each
collapsed directory once (git already declared it wholly ignored, so every file
under it is ignored) to sum its size and file count. Build an immutable folder
tree, aggregating up each folder. A collapsed directory becomes a single
`IsFullyIgnored` node with **no enumerated children** (so a huge `node_modules`
is one node, not tens of thousands) — it is not drilled into. Sizing is eager
and runs on the background scan. Files that cannot be statted count as size 0 and
are tallied as `UnreadableFileCount`.

**SCAN-3** Scans run on a background thread with progress reporting and
cancellation; the UI stays responsive. Sizing a large tree (e.g. a big
`node_modules`) is the dominant cost. The scanner exposes `IProgress<ScanProgress>`
(a running file count; the total is unknowable up front because directories are
collapsed), but **the view model deliberately does not route progress into the
status line**.
*Why:* `Progress<T>` delivers callbacks asynchronously, so a late progress
callback can overwrite a status set after it — observed as a genuinely flaky test
and, in the app, would have left the status stuck on "Scanning…" after an
operation finished. Guarding the callback with a "is this still the active scan"
check was rejected: a callback can still land inside the window before the guard
clears. The indeterminate busy bar (UI-4) signals activity instead.

---

## UI — Interface

**UI-1** Three-pane File-Explorer layout: **repository list** (left), **folder
tree** (middle) showing aggregate ignored size per folder, and **file list**
(right) showing per-file size for the selected folder. A wholly-ignored
directory (SCAN-2) shows as a single marked node. When a repo has **no** ignored
files the tree is left **empty** rather than showing a bare root node — there is
nothing to browse or delete, and the status line says so.

**UI-2** Tri-state checkboxes select folders (cascading to all descendants) and
individual files. Checking a folder selects everything ignored beneath it. A
wholly-ignored directory is a selection leaf: checking it selects the whole
directory (its entire size / file count) as one delete unit.

**UI-3** Show the running total reclaimable size for the current selection.

**UI-4** A unified **Stop** button cancels whatever is running (discovery, scan,
or deletion). An **Undo last delete** button is present (DEL-4). A single
indeterminate busy bar signals any long-running operation; when idle it is
`Hidden`, **not** `Collapsed`, so its row keeps its height and the controls below
never jump.
*Why:* WPF's built-in `BooleanToVisibilityConverter` only yields `Collapsed`
(which removes the element from layout and shifts everything under it), so the
bar uses a style trigger instead. The converter is still used for markers that
never toggle at runtime.

---

## DEL — Deletion

**DEL-1** Delete = move the selected targets to the Recycle Bin via
`SHFileOperation` with `FOF_ALLOWUNDO` (+ no-UI flags), batched per shell
operation (chunked for very large selections). Targets are:
- each **individually-checked file**, and
- each **checked wholly-ignored directory as a single unit** — safe because
  SCAN-1 guarantees such a directory holds no tracked files.

A folder is **never** deleted wholesale unless git collapsed it (i.e. it is
wholly ignored). Mixed folders that also hold tracked files are only ever touched
at the individual-file level. Non-existent targets (already deleted) are skipped.

**DEL-2** Confirm before deleting, showing file count and total size.

**DEL-3** After a delete, re-scan the repository so the tree reflects reality
(deleted targets gone, selection reset).

**DEL-4** Single-level **Undo** restores the last deletion from the Recycle Bin
(match items by original path via `System.Recycle.DeletedFrom`, invoke the shell
Restore verb on an STA thread), then re-scans. Undo is best-effort — if the shell
cannot restore, items remain in the Recycle Bin for manual restore — and is
**forgotten** when the repository is deselected/changed or the app closes.

---

## Open decisions

*(None open — every decision is recorded inline with the rule it belongs to.)*

---

## Backlog (build slices)

All slices are **built and tested** (56 tests; solution builds with 0 warnings
under warnings-as-errors):

- **B1 — Repo discovery** ✅ pick a root folder, list discovered repos (DISC-1/2).
- **B2 — Ignore scan + sizing** ✅ ignored-file tree with sizes (SCAN-1/2/3).
- **B3 — Two-pane browser UI** ✅ tree + file list with sizes (UI-1).
- **B4 — Selection model** ✅ tri-state checkboxes + running total (UI-2/3).
- **B5 — Deletion** ✅ Recycle-Bin delete with confirmation + refresh (DEL-1/2/3).
- **B6 — Robustness** ✅ missing-git, permission/locked-file errors, cancel
  mid-scan, empty results, global unhandled-exception handler.

### Later enhancements (built after the initial delivery)

- **Three-pane UI** — repositories moved from a dropdown to a left-hand list (UI-1).
- **Wholesale folder deletion** — `--directory` scan + delete wholly-ignored
  directories as a unit, fixing the "thousands of files take ages" performance
  problem (SCAN-1/2, DEL-1).
- **Stop** (unified cancel) and single-level **Undo** (DEL-4, UI-4).

### Possible future enhancements (not yet scoped)

- Remove empty directory husks left in *mixed* folders after deleting their
  individual ignored files (wholly-ignored folders are already removed as a unit).
- Remember the last-scanned folder between sessions.
- Lazy drill-down into a wholly-ignored directory (currently shown as one node).
