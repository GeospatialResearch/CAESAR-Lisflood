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
