using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RestartProcess
{
    public partial class RestartProcessForm : Form
    {
        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            NULL = 0,
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_CONTINUOUS = 0x80000000,
        }

        [DllImport("kernel32.dll")]
        private extern static EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public RestartProcessForm()
        {
            InitializeComponent();

            var timer = new Timer();
            timer.Tick += new EventHandler(this.OnSetThreadExecutionStateTimer);
            timer.Interval = 10 * 1000;
            timer.Start();

            notifyIcon1.Text = "RestartProcess";

            // Reset all settings when press SHIFT key...
            if (GetKeyState((int)0x10) < 0)
            {
                config.Reset();
            }

            if (config.RunAutomaticallyAtStartup)
            {
                StartMonitoring();
            }

            UpdateInfo();
        }

        private void OnSetThreadExecutionStateTimer(object sender, EventArgs e)
        {
            // prevent display from turning off
            SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED);
        }

        //
        // see also... http://dobon.net/vb/dotnet/form/hideformwithtrayicon.html
        //
        protected override CreateParams CreateParams
        {
            [System.Security.Permissions.SecurityPermission(
                System.Security.Permissions.SecurityAction.LinkDemand,
                Flags = System.Security.Permissions.SecurityPermissionFlag.UnmanagedCode)]
            get
            {
                const int WS_EX_TOOLWINDOW = 0x80;
                const long WS_POPUP = 0x80000000L;
                const int WS_VISIBLE = 0x10000000;
                const int WS_SYSMENU = 0x80000;
                const int WS_MAXIMIZEBOX = 0x10000;

                CreateParams cp = base.CreateParams;
                cp.ExStyle = WS_EX_TOOLWINDOW;
                cp.Style = unchecked((int)WS_POPUP) |
                    WS_VISIBLE | WS_SYSMENU | WS_MAXIMIZEBOX;
                cp.Width = 0;
                cp.Height = 0;

                return cp;
            }
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------

        private void OpenFileDialog()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.InitialDirectory = @"C:\";
            dlg.Filter = "Executable file(*.exe)|*.exe|All files(*.*)|*.*";
            dlg.Title = "Please select an executable file";
            dlg.RestoreDirectory = true;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                config.TargetProcessPath = dlg.FileName;
                UpdateInfo();
            }
        }

        private void targetProcessNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TargetProcess == null)
            {
                OpenFileDialog();
            }
        }

        private void startProcessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (config.TargetProcessPath == null || config.TargetProcessPath == "")
            {
                OpenFileDialog();
            }

            StartMonitoring();
        }

        private void stopProcessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopMonitoring();
        }

        private void runAutomaticallyAtStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            config.RunAutomaticallyAtStartup = !config.RunAutomaticallyAtStartup;
            UpdateInfo();
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            Application.Exit();
        }

        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------

        private Config config = new Config();

        private Process TargetProcess = null;
        private DateTime LastExecuteTime;


        private void UpdateInfo()
        {
            config.Save();

            if (config.TargetProcessPath == null || config.TargetProcessPath == "")
            {
                targetProcessNameToolStripMenuItem.Text = "TargetProcess: None";
            }
            else
            {
                targetProcessNameToolStripMenuItem.Text = "TargetProcess: " + config.TargetProcessPath;
            }

            if (TargetProcess != null)
            {
                startProcessToolStripMenuItem.Enabled = false;
                stopProcessToolStripMenuItem.Enabled = true;
            }
            else
            {
                startProcessToolStripMenuItem.Enabled = true;
                stopProcessToolStripMenuItem.Enabled = false;
            }

            runAutomaticallyAtStartupToolStripMenuItem.Checked = config.RunAutomaticallyAtStartup;

            if (config.RunAutomaticallyAtStartup)
            {
                RegisterStartup();
            }
            else
            {
                UnregisterStartup();
            }
        }

        private void RegisterStartup()
        {
            Microsoft.Win32.RegistryKey r;
            r = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            r.SetValue(Application.ProductName, Application.ExecutablePath);
            r.Close();
        }

        private void UnregisterStartup()
        {
            Microsoft.Win32.RegistryKey r;
            r = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            try
            {
                r.DeleteValue(Application.ProductName);
            }
            catch(Exception)
            {

            }
            r.Close();
        }

        private void StartMonitoring()
        {
            StopMonitoring();
            Execute();
            UpdateInfo();
        }

        private void StopMonitoring()
        {
            if (TargetProcess != null)
            {
                try
                {
                    TargetProcess.Kill();
                }
                catch (Exception)
                {
                }
                TargetProcess.Dispose();
                TargetProcess = null;
            }

            UpdateInfo();
        }

        private void processExitedHander(object sender, EventArgs e)
        {
            if (LastExecuteTime != null && DateTime.Now.Subtract(LastExecuteTime).TotalSeconds < 2)
            {
                StopMonitoring();
                MessageBox.Show("The process startup interval is too fast. Stop automatic restart.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Execute();
        }

        void Execute()
        {
            if (config.TargetProcessPath == null || config.TargetProcessPath == "")
            {
                MessageBox.Show("Please select target process...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LastExecuteTime = DateTime.Now;
            try
            {
                TargetProcess = new System.Diagnostics.Process();
                TargetProcess.StartInfo.FileName = config.TargetProcessPath;
                TargetProcess.SynchronizingObject = this;
                TargetProcess.Exited += new EventHandler(processExitedHander);
                TargetProcess.EnableRaisingEvents = true;
                TargetProcess.Start();
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to start process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopMonitoring();
            }
        }

    }
}
