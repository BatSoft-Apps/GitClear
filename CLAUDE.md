# GitClear — project bindings

A Windows WPF tool that reclaims disk space by moving git-ignored files to the
Recycle Bin. Complete and shipping; changes arrive as user requests, not slices.

## Design documents

- **`Specifications/DESIGN.md`** — the single design document. All specifications
  live in `Specifications/`; split further files in there if one area outgrows a
  screen.
- Rationale lives **inline with the rule it belongs to**; there is no separate
  decision log.

## User documentation

- **`Documentation/USER-GUIDE.md`** — the end-user guide. Keep it in step with
  on-screen labels and status messages; it quotes them verbatim.
- **Scope (the user's direction; recorded 2026-09-18):** the UI and its messages are clear, so
  the guide covers only what they *don't* show — why some folders behave
  differently, Undo's limits, what is never deleted, the permanent-delete warning.
  No "before you start" or "window at a glance" tours; a troubleshooting entry only
  where the cause adds something beyond the message itself. Plain language: no
  design-doc terms ("wholly-ignored", "mixed", "collapsed"), and any tool-specific
  name (e.g. `node_modules`) explained at first use.
- `Documentation/` is for audience-facing docs; `Specifications/` is for design. Do
  not mix them.
- **`Documentation/USER-GUIDE.pdf`** is generated from the markdown — never edit it
  by hand. Regenerate after any guide change (no pandoc needed; uses Edge headless):

```
python Documentation/md-to-html.py Documentation/USER-GUIDE.md %TEMP%\ug.html "GitClear — User Guide"
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless --disable-gpu --no-pdf-header-footer --print-to-pdf="Documentation\USER-GUIDE.pdf" "file:///%TEMP%/ug.html"
```

  *Why local:* converting through an online PDF service would upload the guide
  for a job that doesn't need it, and pandoc or a Python `markdown` package would
  mean installing software on the machine. The small converter plus the browser
  that ships with Windows avoids both.
  `md-to-html.py` handles only the markdown constructs listed in its docstring;
  extend it if the guide grows new syntax.

## Rule-id families

All owned by `Specifications/DESIGN.md`:

| Family | Area |
|---|---|
| `PROD-n` | product flow |
| `ARCH-n` | architecture / layering |
| `DISC-n` | repository discovery |
| `SCAN-n` | ignore scan & sizing |
| `UI-n`   | interface |
| `DEL-n`  | deletion & undo |

Never renumber or reuse an id — append.

## Not internationalised

Don't assume localisation exists: the UI is English-only. The facts, the
drop-in mechanism for translated guides, and a locale defect in Undo are in
DESIGN.md — Open decision 1, UI-5, and *Known limitations and unverified paths*.

## Ledgers

Both are sections of `Specifications/DESIGN.md`:

- **Open decisions** — open *design* questions.
- **Backlog** — the status ledger: built slices, later enhancements, known
  limitations and unverified paths, and possible future enhancements.

Changes here arrive as user requests, so build-slice rarely runs — but its ledger
step still applies: **every change updates the Backlog in the same turn** (the built
item with its rule ids; limitations and unverified paths under *Known limitations*).
Four changes once went unrecorded until the next pre-compaction ritual.

## Build & test

```
dotnet build GitClear.slnx          # must be 0 warnings (warnings-as-errors; `var` is an error)
dotnet test  GitClear.slnx          # every test must pass
dotnet run --project src/GitClear.App/GitClear.App.csproj
```

Run the WHOLE suite, not just the tests you touched.

## The safety invariant — do not break this

**Never delete a directory wholesale unless `git ls-files --directory` collapsed
it** (i.e. git reported it as a single `foo/` entry, proving it holds no tracked
files). Mixed folders — those with tracked content alongside ignored files — are
only ever touched at the individual-file level. See DEL-1 / SCAN-1.

## Environment gotchas (learned the hard way)

- **.NET 10 creates `GitClear.slnx`**, not `.sln`. `dotnet build GitClear.sln` fails.
- **The WPF markup-compile pass does not run CommunityToolkit's generator for
  *partial properties*.** `[ObservableProperty] public partial T X { get; private set; }`
  fails with CS9248 there. Use the field form, or hand-write the property with
  `SetProperty` when you need a private setter (as `SelectionTracker` does).
- **WPF markup-compile (`*_wpftmp.csproj`) does not reliably get implicit usings.**
  Files in `GitClear.App` need an explicit `using System.IO;` even though
  `ImplicitUsings` is enabled — otherwise `Directory`/`DirectoryNotFoundException`
  fail to resolve only during the XAML pass.
- **Line endings are LF** — the repository and `.editorconfig` agree. On Windows,
  Python's `write_text` silently turns `\n` into CRLF, so scripted edits must write
  bytes. (`.editorconfig` said `crlf` until 2026-09-18, and `dotnet format` then
  rewrote whole files.)
- **An override keeps the framework's parameter names** (`OnStartup(StartupEventArgs e)`):
  CA1725 fails the build otherwise.
- **Checking the real window with UI Automation:** the delete confirmation is a
  Win32 `MessageBox` whose buttons expose no Invoke pattern — click OK by posting
  `BM_CLICK` to its window handle. Windows PowerShell 5.1 reads a `.ps1` without a
  byte-order mark as ANSI, so keep such scripts ASCII.
- **Test projects relax two analyzers** via `<NoWarn>CA1707;CA1861</NoWarn>`:
  underscore test names and inline expected-value arrays are idiomatic in tests.
- **Recycle-Bin tests recycle *then restore*** (ARCH-5 has the why) — keep that
  shape.
- Git may report "dubious ownership" on the `Z:` mapped drive; that affects ad-hoc
  `git` calls, not the app (which runs git inside the scanned repo). For a read-only
  look, `git -c safe.directory='*' status` works without touching the global config.

## Working agreements

- Never commit or stage — the user controls all commits.
- The user's default is confirm-first, **except** on this project, where they
  granted standing autonomy to build end-to-end without per-change sign-off.
