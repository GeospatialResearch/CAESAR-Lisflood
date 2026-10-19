# CAESAR-Lisflood CLI: Phase 0 (Baseline) and Phase 1 (GUI-Side Changes)

**Branch:** `commandline`, created from `Temperature`
**Change tag:** `CLI_V1` (searchable in comments, following the `TEMP_V1` / `WATERINIT_V1` convention)
**Code reviewed against:** `Temperature` head `8a11bac` (code identical to `39d6aa9`; `8a11bac` changes docs only)
**Date:** 2026-10-07

---

## 1. Context

This document covers the first two phases of the command-line (headless) work described in `TEMP_V1_Implementation_Status.md` §5 item 1 and §7 "Headless batch running". The wider rationale, including the choice of a separate CLI executable (Option C) and the longer-term .NET 10 and Linux plans, is in `CAESAR-Lisflood_Platform_Modernisation_Plan.md`.

The design in brief:

- A separate console executable (`caesar-cli`, phase 2) constructs `Form1` without ever showing it, and drives the same methods the GUI buttons drive: open config, load data, start, quit and save.
- All new code lives in new files. The main file `Caesar Lisflood 2.0.cs` receives about 20 changed lines, to keep the merge footprint small (Temperature into main, the oil spill branch, and changes from Tom Coulthard's group).
- Parameter overrides (phase 2) are applied to the XML before it is loaded, so they go through the existing loader unchanged.

Phase 0 captures a baseline. Phase 1 makes every change needed inside the GUI project, and must leave GUI behaviour bitwise identical to the baseline. Phase 2 (the CLI project itself) adds no further edits to the main file.

## 2. Ground rules

1. **Numerics-neutral.** No change to physics or to the timestep sequence. The acceptance test for each phase is a bitwise comparison against the baseline.
2. **One commit per phase.** Never combine a CLI phase with a physics change (in particular the relaxation reformulation), so that harness changes and physics changes can be attributed separately.
3. **Same machine, same build configuration, same launch method for every bitwise comparison.**
   - Same machine: the Windows C runtime selects different code paths for functions such as `exp`, `log` and `pow` depending on CPU features, so results can differ in the last bit between machines. Baselines are per machine.
   - Same build configuration: Debug (`Optimize=false`) and Release produce different JIT code.
   - Same launch method: launch without the debugger (Ctrl+F5, or run the exe directly). Running under the Visual Studio debugger suppresses JIT optimisation by default, and the phase 2 CLI will always run outside the debugger.
4. **Do not reformat or re-indent existing code.** Every reformatted line is a potential merge conflict with the oil spill branch and with Tom's group's code.
5. **Commit and push before switching machines** (the recurring risk recorded in the status document §8).

---

## 3. Phase 0: Baseline

### 3.1 Decide on clock bound (c) before branching

The fixed-cycle / end-time clock bound (c) from status document §7 is **not** in the code at `8a11bac` (there is no `nextTarget` anywhere). It is a numerics change, so it belongs on `Temperature`, not on `commandline`.

- If (c) is to land before the CLI work, commit it on `Temperature` first, then branch, then capture the baseline.
- If it is deferred, branch now and land (c) on `Temperature` after the CLI has been accepted. The CLI baseline is then re-captured after (c).

Either way, **the baseline must be captured on exactly the commit that `commandline` branches from.**

### 3.2 Create the branch

```
git checkout Temperature
git pull
git status                       # must be clean
git checkout -b commandline
git push -u origin commandline
git rev-parse --short HEAD       # record this hash: it names the baseline
```

### 3.3 Choose and prepare the baseline cases

Three cases are recommended. Phase 1 touches code on the temperature path, the general run path, and `erode()` (the `BeginInvoke` guard), so the set must include a run with sediment transport active.

| Case | Source config | Purpose | Changes for the baseline copy |
|---|---|---|---|
| B1 | `testing/squaredem-temperature.xml` | Full energy-balance scheme, stage input, initial depth raster | Settings below only |
| B2 | Copy of B1 | Simplified scheme | Also set the simplified scheme on the Water Quality tab |
| B3 | `testing/Carlisle/carlisle15m load file.xml` (or another short reach-mode run) | Temperature off, sediment on (`grass-sediment` false, so `erode()` runs), tracers on. Also exercises the loader's missing-element path, as this XML predates TEMP_V1 | Reduce `maxrunduration` (hours) so the run takes a few minutes |

Settings for every case. Open the source config in the GUI, change these, then use Save As to write the copy:

- **Output directory:** a distinct absolute path per case, for example `D:\caesar_baseline\B1\out`. Note that `OutputDirectory` is resolved against the current working directory if relative.
- **Date-time subfolder (`OutDirDateTime`):** off, so the output path is deterministic.
- **Google Earth animation:** off. It is produced from the GUI's screen-update timer, so it depends on wall-clock timing, and it will not be produced in headless mode at all (see §5).
- Leave all other settings as they are normally used.

Save the copies as `testing/cli_baseline/B1.xml`, `B2.xml`, `B3.xml`. They contain absolute, machine-specific paths. That is acceptable for the baseline; portable relative-path test configs are a phase 2 task.

### 3.4 Run procedure (each case, twice)

Run each case **twice** (run1, run2). The second run is the repeatability check in §3.6.

1. Build in the chosen configuration (Debug or Release) and record which one was used.
2. Delete the previous outputs:
   - the case's output directory contents;
   - the four debug traces in the working directory (the exe's folder, for example `bin\Debug`): `temp_debug_trace.csv`, `temp_debug_trace2.csv`, and the two `_simplified` variants. These append across runs.
3. Note the working directory's file list, so that any other file the run writes there can be identified afterwards.
4. Launch the exe **without the debugger** (Ctrl+F5, or run the exe directly).
5. Run it:
   1. File > Open config, and choose `Bn.xml`.
   2. Click **load data**, then dismiss the "Variables check" box.
   3. Click **Start!**.
   4. Wait for the "finished. Runtime: ..." box, and dismiss it.
   5. Click **Quit and save**. This writes the final outputs and closes the application.
6. Copy the output directory contents, the four trace files, and any other new files in the working directory into `D:\caesar_baseline\<hash>\Bn\runK\`.
7. Write a hash manifest for that folder (§3.5).

Keep the baseline outputs out of git (they can be large). Commit only the three configs and the manifests, under `testing/cli_baseline/`.

### 3.5 Hash manifest and comparison

Save this as `testing/cli_baseline/hash-dir.ps1`:

```powershell
# Writes a sorted SHA-256 manifest of every file under $Dir, with paths relative to $Dir.
param([Parameter(Mandatory=$true)][string]$Dir,
      [Parameter(Mandatory=$true)][string]$Out)
$root = (Resolve-Path $Dir).Path.TrimEnd('\')
Get-ChildItem -Path $root -File -Recurse | Sort-Object FullName | ForEach-Object {
    $h = Get-FileHash -Path $_.FullName -Algorithm SHA256
    "{0}  {1}" -f $h.Hash, $_.FullName.Substring($root.Length + 1)
} | Set-Content -Encoding UTF8 $Out
```

Usage and comparison:

```powershell
.\hash-dir.ps1 -Dir D:\caesar_baseline\<hash>\B1\run1 -Out B1_run1.sha256
.\hash-dir.ps1 -Dir D:\caesar_baseline\<hash>\B1\run2 -Out B1_run2.sha256
Compare-Object (Get-Content B1_run1.sha256) (Get-Content B1_run2.sha256)
# No output means the two runs are bitwise identical.
```

When a file differs, `fc /b` on the pair shows where.

### 3.6 Interpreting the repeatability check

This check matters because `depth_update()` has several cells writing the same `dhdt_y` entries from different rows under `Parallel.For`, which is benign only while the values written are genuinely identical (status document §7, "Bitwise repeatability").

- **run1 and run2 identical:** the baseline is established. Use run1 as the reference.
- **Differences confined to non-numeric metadata** (a timestamp or runtime written into a file): record which file and which line. That file is then compared textually with that line excluded, both here and in all later phases.
- **Numeric differences:** stop. The bitwise acceptance test cannot be used until this is understood. Record it in the status document as a finding. Bitwise comparison then gives way to a tolerance comparison for the affected outputs, pending investigation of the parallel write.

### 3.7 Record

Add a short entry to the status document: baseline commit hash, machine, build configuration, the three cases, and the repeatability result.

---

## 4. Phase 1: GUI-Side Changes (one commit)

### 4.1 Summary of edits

| # | Location | Edit | Lines changed |
|---|---|---|---|
| 1 | `Form1` class declaration | Add `partial` | 1 |
| 2 | New file `Form1.Headless.cs` | Headless flag, `Notify`, `MessageBox` shim, public entry points | New file |
| 3 | Project file | Include the new file | 3 |
| 4 | `menuItemConfigFileOpen_Click` | Split into the dialog part and `load_config_file(string path)` | About 10 |
| 5 | `main_loop` | Guard `CreateGraphics()` and `gameClock.Start()`; synchronous `erodedepo()` when headless | 3 |
| 6 | `erode()` | Guard `BeginInvoke` | 1 |
| 7 | `button1_Click` | Guard `this.Close()` | 1 |
| 8 | Four message calls | Explicit `Notify` levels | 4 |

**How the `MessageBox` shim works.** `Form1.Headless.cs` declares a nested class named `MessageBox` inside `Form1`. C# name lookup finds a nested type before a type imported by `using System.Windows.Forms;`, so every existing unqualified `MessageBox.Show(...)` inside `Form1` is routed through the shim **without editing the 65 call sites**. In GUI mode the shim calls the real `System.Windows.Forms.MessageBox.Show` with the same text, so behaviour is identical. Any `MessageBox.Show` arriving in future merges is routed automatically too.

### 4.2 Edit 1: make `Form1` partial

**File:** `Caesar Lisflood 2.0.cs`, near line 76, directly after the `/// Summary description for Form1.` comment.

Find:

```csharp
    public class Form1 : System.Windows.Forms.Form
```

Replace with:

```csharp
    public partial class Form1 : System.Windows.Forms.Form // CLI_V1: partial, see Form1.Headless.cs
```

### 4.3 Edit 2: add `Form1.Headless.cs`

Create `Form1.Headless.cs` in the project root, alongside `Caesar Lisflood 2.0.cs`, with this content:

```csharp
// CLI_V1 - headless (command-line) support for Form1.
//
// This file is a partial of Form1 (see "Caesar Lisflood 2.0.cs"). It contains no physics and
// changes nothing in GUI mode: in GUI mode headlessInstance stays null and every message box
// behaves exactly as before.
//
// Headless mode is driven by the separate CLI project (phase 2). It creates a Form1 through
// CreateHeadless(), never shows it, and calls the Headless* methods below in the same order a
// GUI user clicks: open config, load data, Start!, Quit and save.

using System;
using System.Windows.Forms;

namespace caesar1
{
    /// <summary>Severity of a model message. Only affects headless mode.</summary>
    public enum NotifyLevel
    {
        Info,       // purely informational, e.g. "finished"
        Advisory,   // the "Variables check" summary: soft advice, never fails a run
        Warning,    // something went wrong and the model is continuing with a fallback
        Error       // the run cannot meaningfully continue
    }

    /// <summary>Thrown in headless mode to abort on an Error, or on a Warning in strict mode.</summary>
    public class HeadlessAbortException : Exception
    {
        public NotifyLevel Level { get; private set; }
        public HeadlessAbortException(NotifyLevel level, string message) : base(message) { Level = level; }
    }

    public partial class Form1
    {
        // The single headless instance, or null in GUI mode. Static because the MessageBox shim
        // below is a static class and has to find the instance.
        private static Form1 headlessInstance = null;

        private bool headless = false;
        private Action<NotifyLevel, string> headlessLog = null;
        private readonly object notifyLock = new object();
        private int headlessWarningCount = 0;

        /// <summary>When true, a Warning aborts the run instead of continuing with its fallback.</summary>
        public bool HeadlessStrict { get; set; }

        /// <summary>Number of Warning-level messages raised so far in this headless run.</summary>
        public int HeadlessWarningCount { get { lock (notifyLock) { return headlessWarningCount; } } }

        /// <summary>Current model time in minutes, for progress reporting.</summary>
        public static double HeadlessCycle { get { return cycle; } }

        /// <summary>Current iteration count, for progress reporting.</summary>
        public int HeadlessIteration { get { return counter; } }

        /// <summary>Create the (never shown) form used for a headless run.</summary>
        public static Form1 CreateHeadless(Action<NotifyLevel, string> log)
        {
            if (headlessInstance != null)
                throw new InvalidOperationException("Only one headless Form1 is allowed per process.");
            Form1 f = new Form1();      // runs InitializeComponent only; no window handle is created
            f.headless = true;
            f.headlessLog = log;
            headlessInstance = f;
            return f;
        }

        // The four GUI actions, in order.
        public void HeadlessLoadConfig(string xmlPath) { load_config_file(xmlPath); }

        public bool HeadlessLoadData()
        {
            button2_Click(this, EventArgs.Empty);
            return start_button.Enabled;    // set true only when read_header() succeeded
        }

        public void HeadlessRun() { main_loop(this, EventArgs.Empty); }        // erodedepo() runs synchronously

        public void HeadlessFinalise() { button1_Click(this, EventArgs.Empty); } // final saves; Close() is skipped

        /// <summary>
        /// Single route for every message to the user. GUI mode: shows the message box exactly as
        /// before. Headless mode: logs, counts warnings, and throws HeadlessAbortException on an
        /// Error, or on a Warning when HeadlessStrict is set.
        /// </summary>
        internal void Notify(string text, NotifyLevel level)
        {
            if (!headless)
            {
                System.Windows.Forms.MessageBox.Show(text);     // identical to the original call
                return;
            }

            bool abort;
            lock (notifyLock)   // messages can be raised from inside Parallel.For loops
            {
                if (level == NotifyLevel.Warning) headlessWarningCount++;
                if (headlessLog != null) headlessLog(level, text);
                abort = (level == NotifyLevel.Error) || (level == NotifyLevel.Warning && HeadlessStrict);
            }
            if (abort) throw new HeadlessAbortException(level, text);
        }

        // CLI_V1 - MessageBox shim.
        //
        // Inside Form1, an unqualified "MessageBox" now resolves to this nested class rather than
        // to System.Windows.Forms.MessageBox, because C# finds nested types before types imported
        // with "using". Every existing MessageBox.Show(text) call in Form1 therefore comes through
        // here, with no edit at the call site, and so does any call arriving in a future merge.
        //
        // GUI mode: passes straight through to the real MessageBox, so behaviour is unchanged.
        // Headless mode: routed to Notify() as a Warning, since nearly every existing message box
        // reports a fallback ("continuing with...", "using the constant value instead").
        //
        // Do NOT write "System.Windows.Forms.MessageBox.Show" anywhere else in Form1: a fully
        // qualified call bypasses this shim and will block a headless run.
        // A call using another overload (caption, buttons) will not compile; route it through
        // Notify() with an explicit level, or add the overload here.
        private static class MessageBox
        {
            public static System.Windows.Forms.DialogResult Show(string text)
            {
                if (headlessInstance == null) return System.Windows.Forms.MessageBox.Show(text);
                headlessInstance.Notify(text, NotifyLevel.Warning);
                return System.Windows.Forms.DialogResult.OK;
            }
        }
    }
}
```

Notes on this file:

- **`DialogResult` is fully qualified on purpose.** `Form` has an instance property called `DialogResult`, and inside the nested class an unqualified `DialogResult.OK` would bind to that property and fail to compile.
- **`HeadlessLoadData()` uses `start_button.Enabled` as its success flag.** `button2_Click` sets it to true only when `read_header()` returns 1. This avoids editing `button2_Click`.
- **Exceptions from inside `Parallel.For`** arrive wrapped in an `AggregateException`. The phase 2 CLI unwraps it.
- **Empty `catch { }` blocks.** If an abort is thrown inside a `try` that has an empty `catch { }`, it is swallowed. The warning count has still been incremented, so the phase 2 CLI also checks `HeadlessWarningCount` at the end of a strict run. This is the belt-and-braces check.

### 4.4 Edit 3: add the file to the project

The easiest route is in Visual Studio: right-click the project, then Add > Existing Item, and choose `Form1.Headless.cs`. Alternatively, edit `CAESAR-lisflood 2.0.csproj` by hand. In the `<ItemGroup>` containing `<Compile Include="Caesar Lisflood 2.0.cs">`, add:

```xml
    <Compile Include="Form1.Headless.cs">
      <DependentUpon>Caesar Lisflood 2.0.cs</DependentUpon>
    </Compile>
```

`DependentUpon` nests the file under the main file in Solution Explorer. Visual Studio may show the file with a Form icon, because it is a partial of a Form. If double-clicking opens an empty designer, use View Code instead. Do **not** add a `DesignerCategory` attribute to the new file, because an attribute on one part of a partial class applies to the whole class and would disable the designer for `Form1`.

### 4.5 Edit 4: split `menuItemConfigFileOpen_Click`

**File:** `Caesar Lisflood 2.0.cs`, near line 18455, directly after `button3_Click`.

The method body is moved into a new method **without re-indenting it**. A bare block brace `{` stands in for the old `if (openFileDialog1.ShowDialog() ...)` brace, so the body's existing closing braces still balance and git sees only the header lines change.

Find (the top of the method, down to and including the `XmlTextReader` line):

```csharp
        private void menuItemConfigFileOpen_Click(object sender, System.EventArgs e)
        {
            XmlTextReader xreader;
            String dum;

            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = workdir;
            openFileDialog1.Filter = "cfg files (*.xml)|*.xml|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = false;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                cfgname = openFileDialog1.FileName;

                xreader = new XmlTextReader(cfgname);
```

Replace with:

```csharp
        private void menuItemConfigFileOpen_Click(object sender, System.EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = workdir;
            openFileDialog1.Filter = "cfg files (*.xml)|*.xml|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = false;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                load_config_file(openFileDialog1.FileName); // CLI_V1
            }
        }

        // CLI_V1 - the body below is the former body of menuItemConfigFileOpen_Click, unchanged.
        // Split out so that headless mode can load a config without the file dialog. The bare
        // block brace below stands in for the old "if (ShowDialog...)" brace, so the body keeps its
        // original indentation and its original closing braces.
        private void load_config_file(string path)
        {
            XmlTextReader xreader;
            String dum;

            {
                cfgname = path;

                xreader = new XmlTextReader(cfgname);
```

Leave everything below this point untouched, down to and including the method's original closing braces just above `// JMW - Config File Save & SaveAs Event Handler`.

Check: the body never refers to `openFileDialog1` (verified), and `dum` is still declared before its uses in the `SaveOptions` section.

### 4.6 Edit 5: `main_loop` guards

**File:** `Caesar Lisflood 2.0.cs`, method `main_loop`, near lines 6688 to 6728.

Find:

```csharp
            mygraphics = this.CreateGraphics();
```

Replace with:

```csharp
            if (!headless) mygraphics = this.CreateGraphics(); // CLI_V1: would create a window handle
```

Find (near the end of `main_loop`):

```csharp
            gameClock.Start();

            backgroundWorker1.RunWorkerAsync();
```

Replace with:

```csharp
            if (!headless) gameClock.Start(); // CLI_V1: screen updates only, no message loop in headless mode

            // CLI_V1: headless mode runs the main loop synchronously on the calling thread, so the
            // CLI knows when the run has finished and any exception reaches it (in GUI mode an
            // exception inside BackgroundWorker.DoWork is silently swallowed).
            if (headless) erodedepo(); else backgroundWorker1.RunWorkerAsync();
```

`mygraphics` is used only by `drawwater()`, which is called from the screen-update timer and the visualisation menu handlers. Neither runs in headless mode.

### 4.7 Edit 6: `erode()` guard

**File:** `Caesar Lisflood 2.0.cs`, method `erode()`, near line 15569, after the `Parallel.For` that queues status messages.

Find:

```csharp
                this.BeginInvoke((Action)(() => this.tempStatusPanel.Text = latest));
```

Replace with:

```csharp
                if (!headless) this.BeginInvoke((Action)(() => this.tempStatusPanel.Text = latest)); // CLI_V1: BeginInvoke throws without a window handle
```

### 4.8 Edit 7: `button1_Click` guard

**File:** `Caesar Lisflood 2.0.cs`, method `button1_Click` ("Quit and save"), near line 17950. This line is unique in the file.

Find:

```csharp
            this.Close();
```

Replace with:

```csharp
            if (!headless) this.Close(); // CLI_V1: headless reuses the final-save block above
```

### 4.9 Edit 8: explicit message levels at four sites

In GUI mode `Notify` shows exactly the same box as before. These four changes only affect how headless mode classifies the messages. Each "Find" string is unique in the file.

| Location | Find | Replace with |
|---|---|---|
| End of `erodedepo()`, near line 7142 | `MessageBox.Show($"finished. Runtime: {watch.Elapsed} ");` | `Notify($"finished. Runtime: {watch.Elapsed} ", NotifyLevel.Info); // CLI_V1` |
| End of `load_source_temp_file()`, near line 9944 | `MessageBox.Show(mappingReport);` | `Notify(mappingReport, NotifyLevel.Info); // CLI_V1` |
| End of `button2_Click`, near line 18179 | `MessageBox.Show(message);` | `Notify(message, NotifyLevel.Advisory); // CLI_V1` |
| `outDirCheck()` catch block, near line 18232 | `MessageBox.Show("Error with output folder : "` (start of the line) | `Notify("Error with output folder : "` (rest of the line unchanged, with `MessageBox.Show(` replaced by `Notify(` and `, NotifyLevel.Error` added before the closing `);`) |

The output-folder line, once changed, reads:

```csharp
                Notify("Error with output folder : " + textBoxOutDir.Text + "/nFalling back to using current directory for outputs", NotifyLevel.Error); // CLI_V1
```

(The existing `/n` typo, which should be `\n`, is left alone to keep this phase purely structural. Fix it separately if wanted.)

Why these four, and only these:

- **"finished", the mapping report, and "Variables check" appear in every run.** Without explicit levels, `--strict` would fail every run. The "Variables check" box always contains soft advice (Min_Q and so on), hence `Advisory`, not `Warning`.
- **The output-folder fallback silently redirects outputs** to the working directory, which in a scripted test is a wrong-inputs-still-passes failure. It must abort.
- **Everything else stays a `Warning` through the shim.** In phase 2, the CLI additionally treats any `Warning` raised during config or data loading as fatal, regardless of `--strict`. A test that runs with a fallback input is never a valid test.

### 4.10 Check for bypasses

After the edits, from the repository root:

```
findstr /n /c:"System.Windows.Forms.MessageBox" "Caesar Lisflood 2.0.cs"
```

This should find nothing. The only fully qualified calls belong in `Form1.Headless.cs`.

### 4.11 Build and acceptance tests

1. **Build in the same configuration as the baseline.** There should be no new errors. The new file adds no new warnings beyond any the compiler already reports for the main file.
2. **Bitwise GUI test.** Repeat the §3.4 procedure for B1, B2 and B3 (one run each is enough), write manifests, and compare each with the baseline's run1. They must be identical, apart from any metadata line already excluded in §3.6.
3. **GUI behaviour checks:**
   - A fallback warning still shows a message box. For example, point the initial temperature raster at a non-existent file, load data, and confirm the "Initial water temperature raster file not found..." box appears.
   - File > Open config still loads the XML, and the title bar shows the file name.
   - The "Variables check" and "finished" boxes still appear.
   - **Quit and save** still writes the final outputs and closes the application.
   - The visualisation menus still draw.
4. **Commit:**

   ```
   git add "Caesar Lisflood 2.0.cs" Form1.Headless.cs "CAESAR-lisflood 2.0.csproj" testing/cli_baseline
   git commit -m "CLI_V1 phase 1: partial Form1, MessageBox shim and Notify, headless entry points, UI guards (numerics-neutral, GUI bitwise-verified against baseline <hash>)"
   git push
   ```

5. **Update the status document:** add `CLI_V1` to the change-tag list in §8, and record the phase 1 result.

### 4.12 Post-merge checklist (for every future merge into a branch containing `CLI_V1`)

Code merged from main, the oil spill branch, or Tom's group may add UI calls in the run path. These compile and work in the GUI, but hang or throw in headless mode. After each merge, search `Caesar Lisflood 2.0.cs` for:

| Pattern | Why it matters headless |
|---|---|
| `System.Windows.Forms.MessageBox` | Bypasses the shim and blocks the run |
| `.Invoke(` / `.BeginInvoke(` | Throws without a window handle |
| `ShowDialog(` | Blocks the run (acceptable only inside GUI-only event handlers) |
| `CreateGraphics(` | Creates a window handle |
| `Application.DoEvents` | No message loop exists in headless mode |
| `RunWorkerAsync(` | The CLI would return before the run finishes |

Then run the phase 2 CLI smoke test, once it exists. Any hit in code reached from `button2_Click`, `main_loop` or `erodedepo()` needs a `headless` guard or a `Notify` route.

---

## 5. What follows (summary; detailed instructions to come)

**Phase 2: the CLI project (`caesar-cli`).** A new console project in the same solution, referencing the GUI project. This needs no edits to `Caesar Lisflood 2.0.cs`.

- **Project settings must match the GUI exactly**, or the bitwise test fails for reasons unrelated to the code:
  - `AnyCPU`, `Prefer32Bit=false`. New .NET Framework console projects default to `Prefer32Bit=true`, which would run 32-bit with x87 floating point.
  - The same `Optimize` setting per configuration.
  - Its own `app.config` with `gcAllowVeryLargeObjects` (the setting is per process).
  - `[STAThread]` on its `Main`.
- **Command line:**
  - `--input FILE.xml` (required)
  - `--outdir DIR` (sets `OutputDirectory` and forces `OutDirDateTime` to false)
  - `--set NAME=VALUE` (repeatable; `NAME` is the XML element name, or a path for repeated elements)
  - `--strict`
  - `--dry-run`
  - `--help`
- **Override handling:**
  - Overrides are applied to the XML, written as `effective_config.xml` in the output directory, and that file is loaded through `HeadlessLoadConfig`.
  - Override values are type-checked against the original value, because the loader's `TryParse` turns bad values into 0 silently.
  - Unknown or ambiguous element names are an error.
- **Warnings:** any Warning during config or data load is fatal.
- **Exit codes:**

  | Code | Meaning |
  |---|---|
  | 0 | Success |
  | 1 | Usage or override error |
  | 2 | Config or input load error |
  | 3 | Strict-mode warning |
  | 4 | Runtime exception |

- **Output and progress:** `run.log` in the output directory, and a progress line every N wall-clock seconds from a timer thread that reads `HeadlessCycle` and `HeadlessIteration`.
- **Acceptance tests:**
  - CLI against GUI baseline, bitwise (B1 to B3).
  - CLI run twice, bitwise.
  - One test each for `--strict`, a bad override value, an unknown name, and a missing input file (correct exit codes).

**Phase 3: output hygiene.**
- Debug traces go into the output directory, are truncated at run start, and always carry headers.
- Invariant-culture, round-trip number formatting.
- A provenance header (git hash, dirty flag, XML path, effective overrides).

This changes trace file content, so a re-baseline follows.

**Phase 4: configurable trace cells** (an XML element plus `--trace-cell X,Y`), settled before the relaxation step rewrites the debug blocks.

**Known headless limitation:** the Google Earth animation (PNG and KML) is generated by the GUI screen-update timer, so it is not produced in headless mode. Raster and time-series outputs are unaffected.
