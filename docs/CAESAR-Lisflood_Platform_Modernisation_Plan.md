# CAESAR-Lisflood Platform Modernisation: Command-Line Running, .NET 10 and Linux

**Status:** proposal for review
**Prepared for:** Tom Coulthard
**Prepared by:** Geospatial Research Institute Toi Hangarau (GRI), University of Canterbury, Christchurch, NZ
**Date:** 2026-10-07
**Code reviewed:** GitHub `GeospatialResearch/CAESAR-Lisflood`, branch `Temperature`, commit `8a11bac`

---

## 1. Summary

GRI is extending CAESAR-Lisflood 2.0 with a water temperature module (as a foundation for water-quality and pollutant modelling), building on the oil-spill implementation. Two platform needs have come out of that work:

1. **Goal 1 (now):** run CAESAR-Lisflood from the command line on Windows, so that test suites and parameter sweeps can be scripted.
2. **Goal 2 (longer term):** run CAESAR-Lisflood on Linux, in deployable virtual machines or containers, as part of GRI's digital twin work.

Goal 2 cannot be reached by build settings alone. In the current code the main Windows Form *is* the model: parameters are read directly from GUI controls throughout the model code. Reaching Linux therefore needs two things: a move from .NET Framework to modern .NET (currently .NET 10), and a refactor that separates the model from the GUI.

That is a significant change to a shared codebase, and it interacts with several pending merges. We would value your view before planning it in detail.

**Recommendations in brief:**

1. **Proceed now with a command-line executable for Windows** (`caesar-cli.exe`), built as a separate small project that drives the existing, unmodified model code. Its footprint in the main source file is about 20 lines, and it is verified to give results bitwise identical to the GUI. It also provides a regression harness that makes later merges and refactoring verifiable.
2. **Consolidate the code before any structural refactor:** Temperature into main, the oil spill branch, and any changes held by your group that are not yet on GitHub.
3. **Then modernise in verified stages:** a regression suite across model modes, GUI control updates still on .NET Framework, the move to .NET 10, then the model/GUI separation, then the Linux build.
4. **In the meantime, write new code in a "refactor-ready" style** (§9), so that ongoing development does not add to the eventual migration.

Section 12 lists the specific questions we would like your input on.

---

## 2. Background

### 2.1 Branches and pending merges

| Line of development | Status |
|---|---|
| `main` | The shared base on the GRI GitHub repository |
| Oil spill implementation | Developed from the oil-spill fork; to be merged |
| `Temperature` | Water temperature module (TEMP_V1), derived from the oil-spill line; physics validated at function level, with two further physics steps planned (§9); to be merged into main |
| `commandline` | New branch from `Temperature` for the Windows CLI (goal 1) |
| Changes in your group | We understand there may be further developments not yet on GitHub |

### 2.2 Why command-line running is needed now

Current verification work needs many runs per question:

- timestep sweeps;
- fine-timestep reference runs;
- equilibrium tests under constant forcing;
- repeatability checks;
- one test case per input path (reach, catchment, stage/tidal).

Driving these through the GUI is slow and error-prone. A scripted harness also makes it possible to show that a merge or a refactor did not change results. That is directly relevant to the merges above.

### 2.3 Why Linux

The digital twin work needs the model to run unattended on cloud or on-premises Linux infrastructure: configuration and input data in, results out, driven by an orchestration layer. A Windows GUI application is not deployable in that setting.

---

## 3. The current architecture and why it matters

These figures come from inspecting `Caesar Lisflood 2.0.cs`, which is about 20,900 lines, with the form designer code in the same file.

- **The model reads its parameters from GUI controls.** The model code (`erodedepo()` and everything it calls, plus the data loaders) contains about 386 reads of `.Text` or `.Checked`, from about 185 distinct controls. This includes reads inside the main loop, for example `dune_time_box.Text` and `mine_input_textBox.Text`. Menu items carry model settings too: the output save options are `menuItem25.Checked` and similar.
- **The form designer holds the default parameter set.** About 470 `.Text = "..."` assignments in `InitializeComponent` supply the defaults. When an older XML configuration lacks an element, the loader skips it and the designer default stands.
- **The model writes to the GUI, not only reads it.** For example, a failed rain-zonation load deactivates the feature by unticking `checkBox11`, and loading data enables labels and selectors.
- **Messages to the user are modal message boxes** (65 `MessageBox.Show` calls). Most report a fallback ("continuing with ...", "using the constant value instead"). In an unattended run these either block or fail.
- **Display and image output use `System.Drawing`** and a `gdi32` `BitBlt` call. This covers on-screen drawing and the Google Earth PNG and KML animation.
- **The XML configuration is read sequentially** with `XmlTextReader`, element by element in a fixed order, with `try { } catch { }` around optional elements for backward compatibility. Values are parsed with `double.TryParse` in the current culture, so a malformed value becomes 0 silently.

None of this is unusual for a WinForms scientific code that has grown over many years, and it has served CAESAR-Lisflood well. It does mean that the model cannot be compiled without the GUI. That is the central fact behind everything that follows.

---

## 4. Goal 1: the Windows command-line executable

### 4.1 Approach

A WinForms control works without its window ever being created: `.Text` and `.Checked` are ordinary fields until the window exists. The CLI therefore:

1. constructs the main form without showing it;
2. loads the XML through the **existing** loader;
3. calls the same methods as the GUI's **load data**, **Start!** and **Quit and save** buttons;
4. runs the main loop synchronously.

This mirrors a batch mode already present, commented out, in `Form1_Load`.

Parameter overrides (`--set NAME=VALUE`, using the XML element names) are applied to the XML document before it is loaded. Each run writes the resulting `effective_config.xml` to its output directory, which can be reopened in the GUI to reproduce the run exactly.

There are three reasons for overriding at the XML level rather than setting controls after loading:

- **No mapping table.** Every parameter is overridable without one, including parameters added by future merges.
- **Same parse path.** Overrides go through exactly the same code as the GUI. This matters because the loader also sets internal fields directly as it reads, so a later poke at the controls would not reproduce its effects.
- **Independence from the GUI.** XML-level overrides survive the eventual model/GUI separation unchanged.

### 4.2 Options considered for the executable

| Option | Description | Advantages | Disadvantages |
|---|---|---|---|
| A | One GUI exe with a `--headless` switch; attaches to the parent console | Single file; no packaging change | Awkward console behaviour: an interactive prompt returns immediately, output interleaves, and PowerShell needs `Start-Process -Wait` to obtain the exit code. The headless path becomes a Windows-only path to maintain once Linux exists. |
| B | One console-subsystem exe; detaches the console in GUI mode | Single file; correct console behaviour | A console window flashes behind the form on every double-click, and launching the GUI from a prompt blocks that prompt. The same Linux issue as A. |
| C | Separate small console exe (`caesar-cli.exe`) referencing the same code | Correct console behaviour. GUI exe behaviour untouched. **Matches the long-term architecture** (§5): scripts written now use the same command line the Linux build will accept. | Two files deployed side by side; one extra project in the solution |

**Recommendation: Option C.** On Linux the command-line build must exclude WinForms entirely, so a single exe cannot serve both modes in the long run whichever option is chosen. Option C is the first instalment of that end state.

The headless engine is the same code in all three options, so the decision is cheap to reverse. Two build details must match the GUI exactly for results to be bitwise identical:

- **Process bitness.** The console project must be 64-bit with `Prefer32Bit=false`. A 32-bit process uses x87 floating point.
- **Runtime config.** It needs the same `gcAllowVeryLargeObjects` setting.

### 4.3 Footprint and merge impact

The CLI was designed to minimise conflicts with pending merges:

- **New code lives in new files.** The main form becomes a `partial` class, and the headless support goes in a new `Form1.Headless.cs`.
- **The 65 message boxes are intercepted without editing them.** A nested class named `MessageBox` inside the form takes precedence over `System.Windows.Forms.MessageBox` under C# name lookup. In GUI mode it passes straight through, so behaviour is unchanged. In headless mode it logs the message, counts it as a warning, and can fail the run under `--strict`. Message boxes added by future merges are covered automatically.
- **About 20 lines change in the main file:**
  - guards around the few calls that need a window (`CreateGraphics`, `BeginInvoke`, the screen timer, `Close`);
  - a synchronous main loop when headless;
  - the XML-open handler split into a dialog part and a load-from-path method, without re-indenting the 900-line loader body;
  - explicit severity levels at four message sites.
- **The XML loader and saver are not touched.** They are the most frequent merge hotspot, since every feature adds elements to them.

The remaining merge risk is behavioural rather than textual. Merged code could add new UI calls in the run path, such as `Invoke`, dialogs or a fully qualified `MessageBox`. These would compile and work in the GUI but block a headless run. A short post-merge checklist plus a CLI smoke test covers this.

**Acceptance test:** the CLI and the GUI must produce bitwise-identical outputs for the same configuration, on the same machine and build. The same test is used to show that each later stage is numerics-neutral.

**Effort:** about two to four working days including verification, plus about one day for output hygiene: per-run output directories, provenance headers (git commit, configuration, overrides), and round-trip number formatting.

**Known limitation:** the Google Earth animation is generated from the GUI's screen-update timer, so the CLI does not produce it. Raster and time-series outputs are unaffected.

---

## 5. Target architecture for goal 2

| Component | Contents | Target framework |
|---|---|---|
| `Caesar.Core` | `RunConfig` (every user parameter as a typed field, with the current designer values as defaults), `ConfigXml` (reads and writes `RunConfig`, same schema), `CaesarModel` (all state arrays and physics, reading only `RunConfig`, reporting progress and messages through an interface) | `net10.0` (cross-platform) |
| `Caesar.Gui` | The existing WinForms interface, binding its controls to `RunConfig` and drawing from `CaesarModel`'s arrays | `net10.0-windows` |
| `Caesar.Cli` | The goal 1 command-line front end, retargeted from the GUI assembly to `Caesar.Core` | `net10.0` (Windows and Linux) |

The command line, the XML format and the override mechanism stay the same throughout. Users and scripts see no change when the CLI moves from .NET Framework to the Core library.

---

## 6. Moving to .NET 10

### 6.1 Benefits

- **A supported, cross-platform runtime.** .NET 10 is the current Long Term Support release: released 11 November 2025, supported until 14 November 2028 [1]. .NET 8 and .NET 9 both reach end of support on 10 November 2026 [1].
  - The project currently targets .NET Framework 4.6.1, which is itself retired. It runs on the 4.8.x runtime built into Windows, and .NET Framework 4.8.x is supported for as long as the Windows version it ships with.
  - .NET Framework, though, is Windows-only and receives no new development.
- **Linux builds and builds from the command line.** SDK-style projects build with the `dotnet` command on any operating system, which is what an automated Linux build needs.
- **Correct floating-point formatting and parsing.** Since .NET Core 3.0, `double.ToString()` produces the shortest round-trippable form and parsing is IEEE-compliant. On .NET Framework the default format gives 15 significant digits, which does not round-trip. That limits machine-precision comparison against independent reimplementations.
- **Performance.** Runtime and JIT improvements are typical, although we have not measured them for this code.

### 6.2 Costs specific to this code

- **Removed WinForms controls.** Starting with .NET Core 3.1, `MainMenu` and `MenuItem` are no longer available, with `MenuStrip` and `ToolStripMenuItem` as the recommended replacements [2]. `StatusBar` and `StatusBarPanel` were removed too [3].
  - CAESAR-Lisflood uses one `MainMenu` with 36 `MenuItem`s, several of which carry model settings (the save options), and a `StatusBar` with six panels.
  - These must be migrated to `MenuStrip` and `StatusStrip`. Both replacements also exist in .NET Framework, so the migration can be done and verified **before** the runtime change (§6.4).
  - A compatibility package that restores the removed controls on .NET 10 exists as a stopgap [4], but a proper migration is preferable.
- **Default font change.** Starting with .NET Core 3.0, the default WinForms font is Segoe UI 9 pt, where it was Microsoft Sans Serif 8.25 pt [2]. On a dense tabbed form this shifts layouts. The old default can be restored at startup, but the GUI needs a visual check either way.
- **Project file.** The legacy project file must be converted to SDK style. That is quick for a project this size. ClickOnce and manifest settings and the `System.Deployment` reference are dropped, and one unused `using System.Runtime.Remoting...` is removed.
- **Tooling.** It has been reported that targeting .NET 10 requires Visual Studio 2026, and that Visual Studio 2022 cannot [5]. This should be confirmed against the Visual Studio versions in use in both groups, since everyone building the code would need to move together.
- **`System.Drawing` is Windows-only on modern .NET** [6]. That is acceptable for the GUI build. The consequences for Linux are covered in §8.

### 6.3 Numerical results

A runtime change is not bitwise-neutral. JIT code generation differs, and the parsing correction in §6.1 means some configuration values may now parse to a value that differs in the last bit. Results will therefore differ slightly between the .NET Framework and .NET 10 builds. The proposed treatment:

1. Compare the two builds over the regression suite with an explicit, documented tolerance.
2. Investigate any difference beyond it.
3. Adopt a new bitwise baseline on .NET 10.

This runtime change must be a commit of its own, never combined with physics or structural changes.

### 6.4 Recommended sequence and effort

1. **On .NET Framework**, migrate the menus and status bar, convert to an SDK-style project still targeting `net48`, and remove dead references. Each step is verified bitwise against the baseline.
2. **Change the target framework** to `net10.0-windows`. Ideally build `net48` and `net10.0-windows` side by side from identical source for one round, so that the tolerance comparison isolates the runtime change.
3. **Check the GUI layout** and adopt the new baseline.

Rough effort: three to six working days, dominated by GUI checking and re-baselining rather than code changes.

### 6.5 Deployment

- **Windows users** need either the .NET 10 Desktop Runtime installed, or a "self-contained" build that bundles the runtime. The self-contained build is larger, but needs nothing installed.
- **Linux VMs or containers** can use a self-contained build or the official .NET runtime container images.

---

## 7. Separating the model from the GUI

### 7.1 What it involves

The work is the creation of `RunConfig`, `ConfigXml` and `CaesarModel` (§5). The model code then reads parameters from `RunConfig`, and the GUI binds its controls to it.

The physics is moved, not modified. On a fixed runtime and machine, each step should therefore be **bitwise-identical** to the previous one, and the goal 1 harness is what makes that checkable. We would not recommend attempting this refactor without it.

### 7.2 Implications and risks

- **Verification breadth is the main cost.** The refactor touches every part of the model: hydraulics, sediment, slope processes, dunes, soil development, tracers, oil and temperature. Our current tests exercise mainly the temperature and stage-input paths. A regression set covering each model mode needs to be built **before** the refactor starts, captured from the current GUI: catchment and reach modes, sediment on and off, the tracers, oil, and any options in active use. Your guidance on representative cases would be particularly valuable here.
- **Defaults must be carried over exactly.** The designer's default values become `RunConfig` defaults. An equivalence test loads every available configuration file through both the old path (via the controls) and the new path (`ConfigXml`), dumps the resulting parameter sets, and compares them.
- **Some behaviour changes need deliberate decisions:**
  - **Live parameter edits.** Because a few controls are read inside the main loop, editing them during a run currently takes effect live. A configuration snapshot at the start of a run ends that. We think this is desirable, but it may be relied on by users.
  - **Parse culture.** Parsing would move to invariant culture (decimal point regardless of Windows locale). This changes behaviour only on machines with comma-decimal locales, where it is arguably a fix.
  - **Model-to-GUI writes** (such as the deactivation by unticking a box) become explicit state changes reported to the GUI. Each is a small design decision rather than a mechanical edit.
- **Merge conflicts.** The changes touch lines inside nearly every model function of a single very large file. Any branch open while an increment is in progress will conflict heavily with it. This is the main reason for the consolidation recommendation in §9.
- **Divergence.** After the refactor, porting changes from a pre-refactor copy of the code becomes manual. If the refactored version is not adopted as the common base for all groups, the lines of development effectively become separate forks. This is the most important question for you (§12).
- **Line history.** Splitting the file loses simple `git blame` history. It can still be followed with `git blame -C` and `git log --follow`.

### 7.3 Staged route

Each step is verified bitwise against the regression suite:

1. **Make the form class `partial` and split the source file by concern:** designer, GUI handlers, XML input and output, hydraulics, sediment, temperature, oil, tracers. This changes file boundaries only, not code. It is low-risk and makes every later change and merge easier to review. Because it moves nearly every line, it should be scheduled when few branches are open.
2. **Introduce `RunConfig`**, filled from the controls in one function at load and start, and switch the model's reads to it one area at a time. This is the largest step, but each increment is small.
3. **Move the state arrays and physics into `CaesarModel`**, which is no longer a Form. Once step 2 has removed the control references, this is largely mechanical.
4. **Make `ConfigXml` read and write `RunConfig` directly**, keeping the current schema and element names. A redesign of the configuration format, previously deferred, can follow later behind this interface.
5. **Split into the three projects of §5.**

**Effort:** weeks rather than days, dominated by verification. Step 1 is about a day, step 2 is the largest, and step 5 is small if the earlier steps are clean. A firmer estimate depends on the regression suite's scope.

---

## 8. Goal 2: the Linux build

### 8.1 Routes

| Route | Description | Assessment |
|---|---|---|
| Mono | Run the existing Windows-built CLI under Mono, which implements .NET Framework 4.x including its own WinForms | Worth a short experiment once goal 1 works. Creating the (unshown) form probably still needs an X display, for example a virtual framebuffer. Mono's long-term future is uncertain: as we understand it, Microsoft transferred the project to WineHQ in 2024. Not a foundation for deployment. |
| Modern .NET | Build `Caesar.Cli` against `Caesar.Core` for `net10.0` on Linux, after §7 | Recommended. Supported, and buildable in automated pipelines. |

### 8.2 What changes for Linux specifically

- **Image output.** On-screen drawing and the Google Earth PNG and KML animation depend on `System.Drawing`, which is Windows-only on modern .NET [6]. The Linux CLI would initially omit the animation, or later use a cross-platform imaging library (licences to be checked). The ASCII grid and time-series outputs are plain text and are unaffected.
- **Paths.** Linux paths are case-sensitive and use `/`. Configuration files with absolute Windows paths need converting, and relative paths should be preferred in shared test cases.
- **Numerical comparison across platforms.** Results will not be bitwise identical between Windows and Linux, because the maths libraries and code generation differ in the last bit. Cross-platform validation uses a documented tolerance. Bitwise tests remain valid within one platform, runtime and machine, which is where they are used for verifying refactors and merges.
- **Deployment shape.** A self-contained Linux build or container image, with configuration and inputs mounted, outputs written to a mounted volume, exit codes reporting success or failure, logs to standard output, and provenance in every output header. This fits the orchestration layer of a digital twin.

---

## 9. Merge strategy and sequencing alongside ongoing development

The modernisation must be planned around continuing scientific development in both groups. Recommendations:

1. **Get all code into the shared repository first.** Changes held in your group that are not yet on GitHub should be committed, even to a separate branch, before any structural work begins. They can then be compared, merged and covered by the regression suite.
2. **Use the CLI to verify the consolidation merges.** Merging Temperature, the oil spill branch and your group's changes into main is a precondition for the refactor. The CLI harness can show that each merge leaves existing test cases unchanged, or identify exactly what changed.
3. **Write new code "refactor-ready" in the meantime**, so that ongoing development does not increase the migration:
   - physics functions read plain fields or a small parameter object, never controls;
   - parameters are parsed once, at load, in one place;
   - model code does not write to controls;
   - messages go through the `Notify` route rather than `MessageBox.Show`.

   GRI's temperature module already largely follows this pattern: its only control reads in model code are input file paths at load time. The next two planned temperature steps (an exponential-relaxation integration scheme, then bed heat exchange) will follow it, so they add little to the migration.
4. **Schedule structural steps as short, announced windows.** The file split (§7.3 step 1) in particular should happen when few branches are open. Everyone then rebases onto the result, rather than carrying an old branch across it.
5. **Tag a release before the .NET 10 move** (for example `v2.x-netframework`), so that users who need the original runtime have a clear reference point.

### Proposed overall sequence

| Stage | Work | Depends on | Rough effort |
|---|---|---|---|
| 0 | Windows CLI (goal 1), merged into `Temperature` | None | 3 to 5 days |
| 1 | Continue physics development using the refactor-ready conventions; in parallel, consolidate branches into main, verified with the CLI | Stage 0 | Ongoing |
| 2 | Regression suite across model modes, captured from the GUI | Stage 1 consolidation | 1 to 2 weeks, depending on scope |
| 3 | GUI control migration and SDK-style project, still on .NET Framework | Stage 2 | 2 to 3 days |
| 4 | Move to .NET 10; tolerance comparison; new baseline | Stage 3 | 2 to 3 days |
| 5 | Model/GUI separation (§7.3 steps 1 to 4) | Stage 4 | Several weeks |
| 6 | Core, GUI and CLI projects; Linux build; container packaging; cross-platform validation | Stage 5 | 1 to 2 weeks |

**Why the .NET 10 move (stage 4) comes before the separation (stage 5):** the separation is verified bitwise, and this way that verification is done on the runtime the code will keep. Only one tolerance re-baseline is needed overall. Stages 3 and 4 could be done earlier if wanted, since they do not depend on the separation.

All effort figures are rough working estimates, for planning, not commitments.

---

## 10. Verification strategy (applies throughout)

| Comparison | Method | When |
|---|---|---|
| Same code, repeated run | Bitwise | Every baseline (also checks that a known parallel write in `depth_update()` remains benign) |
| CLI against GUI | Bitwise | Goal 1 acceptance |
| Before and after a merge | Bitwise where the merge should not affect a case; otherwise explained differences | Every consolidation merge |
| Before and after a refactor step | Bitwise | Every step of §6.4 step 1 and §7.3 |
| Configuration loading, old against new | Parameter-set dump comparison over all known configuration files | §7.3 step 4 |
| .NET Framework against .NET 10 | Documented tolerance | Stage 4, once |
| Windows against Linux | Documented tolerance | Stage 6, once per release |

Bitwise comparisons are made on the same machine and the same build configuration, because the Windows maths library can select CPU-dependent code paths.

---

## 11. Recommendations

1. Build the Windows CLI now as a separate console executable (Option C), with its minimal footprint and bitwise acceptance test.
2. Consolidate all lines of development into the shared repository and main branch before structural work, using the CLI to verify merges.
3. Adopt the refactor-ready conventions (§9 item 3) for all new development, in both groups.
4. Build a regression suite covering each model mode before any structural change.
5. Modernise in the order of §9: control migration on .NET Framework, the .NET 10 move, the model/GUI separation, then the Linux build. Every step is a separate, verified commit.
6. Keep the XML configuration schema unchanged through the refactor. Consider a format redesign only afterwards.

---

## 12. Questions for review

1. **Common base.** Are you supportive of this modernisation in principle? Would the refactored, .NET 10 version be adopted as the common base for CAESAR-Lisflood development in all groups, or should we plan for it to remain a GRI line? This determines how much the divergence in §7.2 matters.
2. **Unpublished changes.** What changes exist in your group's versions that are not on GitHub, and could they be brought into the repository before structural work begins?
3. **Regression cases.** Which model modes and options are in active use and must be covered? Do you have reference cases, ideally with known results, that we could adopt?
4. **Retirement candidates.** Are there features that could be retired rather than migrated (for example legacy options no longer in use)? Each one removed reduces the refactor's size.
5. **Configuration compatibility.** Must every historical XML configuration continue to load, or is compatibility from a stated version onwards acceptable?
6. **Live parameter edits.** Do users deliberately change parameters in the GUI during a run, relying on them taking effect immediately?
7. **Tooling and runtime.** Which Visual Studio versions are in use in your group? Can users be expected to install the .NET 10 runtime, or should we ship self-contained builds?
8. **Licensing and attribution.** What licence and attribution should accompany redistributable Linux builds and container images?

---

## References

1. Microsoft, ".NET and .NET Core Support Policy": https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
2. Microsoft Learn, "Breaking changes in Windows Forms for .NET Core 3.0 and 3.1": https://learn.microsoft.com/en-us/dotnet/core/compatibility/winforms
3. dotnet/winforms GitHub issue 2091 (removal of `StatusBar` and related types): https://github.com/dotnet/winforms/issues/2091
4. NuGet, "WTG.Windows.Forms" (fork restoring `MainMenu`, `MenuItem`, `StatusBar` and others; `net10.0` compatible): https://www.nuget.org/packages/WTG.Windows.Forms
5. ASP.NET Zero blog, "What .NET 10 LTS Means for Enterprise Applications" (the Visual Studio 2026 requirement; to be confirmed against Microsoft documentation): https://aspnetzero.com/blog/what-.net-10-lts-means-for-enterprise-applications
6. Microsoft Learn, "System.Drawing.Common only supported on Windows" (.NET 6 breaking change): https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only
