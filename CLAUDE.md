# GitClear — project bindings

A Windows WPF tool that reclaims disk space by moving git-ignored files to the
Recycle Bin. Complete and shipping; changes arrive as user requests, not slices.

## Design documents

- **`Specifications/DESIGN.md`** — the single design document. All specifications
  live in `Specifications/`; split further files in there if one area outgrows a
  screen.
- Rationale lives **inline with the rule it belongs to**; there is no separate
  decision log.

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

## Ledgers

Both are sections of `Specifications/DESIGN.md`:

- **Open decisions** — open *design* questions (currently none).
- **Backlog** — implementation slices (all built) plus "Later enhancements" and
  "Possible future enhancements".

## Build & test

```
dotnet build GitClear.slnx          # must be 0 warnings (warnings-as-errors)
dotnet test  GitClear.slnx          # 56 tests, all must pass
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
- **WPF markup-compile (`*_wpftmp.csproj`) does not reliably get implicit usings.**
  Files in `GitClear.App` need an explicit `using System.IO;` even though
  `ImplicitUsings` is enabled — otherwise `Directory`/`DirectoryNotFoundException`
  fail to resolve only during the XAML pass.
- **Test projects relax two analyzers** via `<NoWarn>CA1707;CA1861</NoWarn>`:
  underscore test names and inline expected-value arrays are idiomatic in tests.
- **Recycle-Bin tests recycle *then restore*** a throwaway temp file/folder, so a
  test run leaves no residue in the user's Recycle Bin. Keep that shape.
- Git may report "dubious ownership" on the `Z:` mapped drive; that affects ad-hoc
  `git` calls, not the app (which runs git inside the scanned repo).

## Working agreements

- Never commit or stage — the user controls all commits.
- The user's default is confirm-first, **except** on this project, where they
  granted standing autonomy to build end-to-end without per-change sign-off.
