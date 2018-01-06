﻿// Written by x1nixmzeng for the Cxbx-Reloaded project
//

using System;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;

namespace CxbxDebugger
{
    public partial class Form1 : Form
    {
        Thread DebuggerWorkerThread;
        Debugger DebuggerInst;
        string[] CachedArgs;

        DebuggerFormEvents DebugEvents;

        List<DebuggerThread> DebugThreads = new List<DebuggerThread>();

        public Form1()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();

            // Arguments are expected before the Form is created
            if (args.Length < 2)
            {
                throw new Exception("Incorrect usage");
            }

            var items = new List<string>(args.Length - 1);
            for (int i = 1; i < args.Length; ++i)
            {
                items.Add(args[i]);
                //listBox1.Items.Add("Arg: " + args[i]);
            }

            CachedArgs = items.ToArray();

            DebugEvents = new DebuggerFormEvents(this);

            SetDebugProcessActive(false);

            // TODO: Wait for user to start this?
            //StartDebugging();
        }

        private void StartDebugging()
        {
            bool Create = false;

            if (DebuggerWorkerThread == null)
            {
                // First launch
                Create = true;
            }
            else if (DebuggerWorkerThread.ThreadState == System.Threading.ThreadState.Stopped)
            {
                // Further launches
                Create = true;
            }

            if (Create)
            {
                // Create debugger instance
                DebuggerInst = new Debugger(CachedArgs);
                DebuggerInst.RegisterEventInterfaces(DebugEvents);

                // Setup new debugger thread
                DebuggerWorkerThread = new Thread(x =>
                {
                    if (DebuggerInst.Launch())
                    {
                        DebuggerInst.Run();
                    }
                });

                DebuggerWorkerThread.Name = "CxbxDebugger";
                DebuggerWorkerThread.Start();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StartDebugging();
        }

        private void PopulateThreadList()
        {
            comboBox1.Items.Clear();

            foreach (DebuggerThread Thread in DebugThreads)
            {
                bool IsMainThread = (Thread.Handle == Thread.OwningProcess.MainThread.Handle);
                string DisplayStr = "";

                if( IsMainThread )
                {
                    DisplayStr = string.Format("> [{0}] 0x{1:X8}", (uint)Thread.Handle, (uint)Thread.StartAddress);
                }
                else
                {
                    DisplayStr = string.Format("[{0}] 0x{1:X8}", (uint)Thread.Handle, (uint)Thread.StartAddress);
                }

                comboBox1.Items.Add(DisplayStr);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (DebuggerInst != null)
            {
                DebuggerInst.Break();

                PopulateThreadList();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (DebuggerInst != null)
            {
                DebuggerInst.Resume();
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (DebuggerWorkerThread != null)
            {
                if (DebuggerWorkerThread.ThreadState == ThreadState.Running)
                {
                    DebuggerWorkerThread.Abort();
                }
            }

            if (DebuggerInst != null)
            {
                DebuggerInst.Dispose();
            }
        }

        private void DebugEvent(string Message)
        {
            string MessageStamped = string.Format("[{0}] {1}", DateTime.Now.ToLongTimeString(), Message);

            if (lbConsole.InvokeRequired)
            {
                // Ensure we Add items on the right thread
                lbConsole.Invoke(new MethodInvoker(delegate ()
                {
                    lbConsole.Items.Insert(0, MessageStamped);
                }));
            }
            else
            {
                lbConsole.Items.Insert(0, MessageStamped);
            }
        }

        private void SetDebugProcessActive(bool Active)
        {
            if (btnRestart.InvokeRequired)
            {
                btnRestart.Invoke(new MethodInvoker(delegate ()
                {
                    // Disable when active
                    btnRestart.Enabled = !Active;

                    // Enable when active
                    btnSuspend.Enabled = Active;
                    btnResume.Enabled = Active;
                }));
            }
            else
            {
                // Disable when active
                btnRestart.Enabled = !Active;

                // Enable when active
                btnSuspend.Enabled = Active;
                btnResume.Enabled = Active;
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            lbConsole.Items.Clear();
        }

        class DebuggerFormEvents : IDebuggerGeneralEvents, IDebuggerProcessEvents, IDebuggerModuleEvents, IDebuggerThreadEvents, IDebuggerOutputEvents, IDebuggerCallstackEvents, IDebuggerExceptionEvents
        {
            Form1 frm;

            public DebuggerFormEvents(Form1 main)
            {
                frm = main;
            }

            private string PrettyExitCode(uint ExitCode)
            {
                string ExitCodeString;

                switch (ExitCode)
                {
                    case 0:
                        ExitCodeString = "Finished";
                        break;

                    case 0xC000013A:
                        // Actual code is STATUS_CONTROL_C_EXIT, but isn't very friendly
                        ExitCodeString = "Debug session ended";
                        break;

                    default:
                        ExitCodeString = string.Format("{0:X8}", ExitCode);
                        break;
                }

                return ExitCodeString;
            }

            public void OnProcessCreate(DebuggerProcess Process)
            {
                // !
            }

            public void OnProcessExit(DebuggerProcess Process, uint ExitCode)
            {
                int remainingThreads = Process.Threads.Count;

                frm.DebugEvent(string.Format("Process exited {0} ({1})", Process.ProcessID, PrettyExitCode(ExitCode)));
                frm.DebugEvent(string.Format("{0} thread(s) remain open", remainingThreads));
            }

            public void OnDebugStart()
            {
                frm.SetDebugProcessActive(true);
                frm.DebugEvent("Started debugging session");
            }

            public void OnDebugEnd()
            {
                frm.SetDebugProcessActive(false);
                frm.DebugEvent("Ended debugging session");
            }

            public void OnThreadCreate(DebuggerThread Thread)
            {
                frm.DebugEvent(string.Format("Thread created {0}", Thread.ThreadID));
                frm.DebugThreads.Add(Thread);
            }

            public void OnThreadExit(DebuggerThread Thread, uint ExitCode)
            {
                frm.DebugEvent(string.Format("Thread exited {0} ({1})", Thread.ThreadID, PrettyExitCode(ExitCode)));
                frm.DebugThreads.Remove(Thread);
            }

            public void OnModuleLoaded(DebuggerModule Module)
            {
                frm.DebugEvent(string.Format("Loaded module \"{0}\"", Module.Path));
            }

            public void OnModuleUnloaded(DebuggerModule Module)
            {
                frm.DebugEvent(string.Format("Unloaded module \"{0}\"", Module.Path));
            }

            public void OnDebugOutput(string Message)
            {
                frm.DebugEvent(string.Format("OutputDebugString \"{0}\"", Message));
            }

            // TODO Update DebuggerStackFrame to query symbols
            public void OnCallstack(DebuggerCallstack Callstack)
            {
                foreach (DebuggerStackFrame StackFrame in Callstack.StackFrames)
                {
                    var stackFrameStr = string.Format("{0:X8} --> {1:X8}", (uint)StackFrame.Base, (uint)StackFrame.PC);
                    frm.DebugEvent("Callstack: " + stackFrameStr);
                }
            }

            public void OnAccessViolation()
            {
                frm.DebugEvent("Access violation exception occured");
            }
        }
    }
}
