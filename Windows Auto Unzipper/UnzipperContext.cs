using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Windows_Auto_Unzipper.Properties;

namespace Windows_Auto_Unzipper
{
    /// <summary>
    /// Application context for the program
    /// </summary>
    class UnzipperContext : ApplicationContext
    {
        private Form settingsForm;
        private NotifyIcon trayIcon;
        private FolderWatcher folderWatcher;
        private SynchronizationContext synchronizationContext;

        private ContextMenuStrip menu;
        private ToolStripMenuItem menuItemToggleRunning;
        private ToolStripMenuItem menuItemOpenFolder;
        private ToolStripMenuItem menuItemLastResult;
        private ToolStripMenuItem menuItemSettings;
        private ToolStripMenuItem menuItemExit;
        private string lastResultText = "No recent activity";

        /// <summary>
        /// Stores the directory that the folder watcher is targeting
        /// </summary>
        private string targetDirectory = UserFolders.GetPath(UserFolder.Downloads);

        /// <summary>
        /// Initialize the program
        /// </summary>
        public UnzipperContext()
        {
            this.synchronizationContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            //Create the tray icon
            this.trayIcon = new NotifyIcon();
            this.trayIcon.Icon = Windows_Auto_Unzipper.Properties.Resources.icon_128x128;
            this.trayIcon.Text = "Auto Unzipper";
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += (sender, e) => this.ShowSettings();

            //Initialize the target folder the the users downloads folder on first run
            if (String.IsNullOrEmpty(Settings.Default.TargetFolder))
            {
                Settings.Default.TargetFolder = UserFolders.GetPath(UserFolder.Downloads);
                Settings.Default.Save();
            }

            this.SetTargetDirectory(Settings.Default.TargetFolder);

            //Start the folder watcher depending on the start mode
            if (Settings.Default.StartMode == "Running" || (Settings.Default.StartMode == "Remember from last session" && Settings.Default.LastRunningMode == "Running"))
            {
                this.folderWatcher.Start();
            }


            //Setup the right-click  menu
            this.InitializeContextMenu();

            //Enable auto-run based on saved settings
            if (Settings.Default.AutoLaunch)
            {
                RegistryHelper.EnableAutoRun();
            }
            else
            {
                RegistryHelper.DisableAutoRun();
            }
        }

        /// <summary>
        /// Opens the settings window
        /// </summary>
        public void ShowSettings()
        {
            if (Application.OpenForms.OfType<FormSettings>().Any())
            {
                //If windows form has already been created, show it and bring it to the front

                if (this.settingsForm == null)
                {
                    this.settingsForm = Application.OpenForms.OfType<FormSettings>().First();
                }

                this.settingsForm.Show();
                this.settingsForm.Visible = true;
                this.settingsForm.WindowState = FormWindowState.Normal;
                this.settingsForm.BringToFront();

            }
            else
            {
                //Create the windows form for the first time
                this.settingsForm = new FormSettings(this);
                this.settingsForm.Show();
            }
        }

        /// <summary>
        /// Event handler for when the right-click system tray menu is opening
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menu_Opening(Object sender, CancelEventArgs e)
        {
            if (this.folderWatcher.IsRunning())
            {
                this.menuItemToggleRunning.Text = "Stop";
                this.menuItemToggleRunning.Image = CreateMenuIcon(SystemIcons.Shield, Color.FromArgb(220, 70, 70));
            }
            else
            {
                this.menuItemToggleRunning.Text = "Start";
                this.menuItemToggleRunning.Image = CreateMenuIcon(SystemIcons.Application, Color.FromArgb(60, 145, 90));
            }

            this.menuItemLastResult.Text = this.lastResultText;
        }

        /// <summary>
        /// Event handler for when the Running/Stopped menu item in the right-click menu is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItemToggleRunning_Click(Object sender, EventArgs e)
        {
            if (this.folderWatcher.IsRunning())
            {
                this.folderWatcher.Stop();
                this.menuItemToggleRunning.Text = "Start";
                Settings.Default.LastRunningMode = "Stopped";
                this.SetTrayStatus("Auto Unzipper stopped", ToolTipIcon.Info);
            }
            else if (this.folderWatcher.Start())
            {
                this.menuItemToggleRunning.Text = "Stop";
                Settings.Default.LastRunningMode = "Running";
                this.SetTrayStatus($"Watching {this.targetDirectory}", ToolTipIcon.Info);
            }
            else
            {
                this.SetTrayStatus("Unable to start: target folder is missing.", ToolTipIcon.Warning);
            }
            Settings.Default.Save();
        }

        /// <summary>
        /// Sets the directory that will be watched for new zip files
        /// </summary>
        /// <param name="location"></param>
        public void SetTargetDirectory(String location)
        {
            this.targetDirectory = location;

            if (this.folderWatcher != null)
            {
                this.folderWatcher.SetTargetDirectory(location);
            }
            else
            {
                this.folderWatcher = new FolderWatcher(this);
            }
        }

        /// <summary>
        /// Get the path to the directory that the file watcher is watching for new zip files
        /// </summary>
        /// <returns></returns>
        public String GetTargetFolder()
        {
            return this.targetDirectory;
        }

        public void ReportExtractionResult(ExtractionResult result)
        {
            this.synchronizationContext.Post(_ =>
            {
                string fileName = System.IO.Path.GetFileName(result.ArchivePath);
                this.lastResultText = result.Success ? $"Last: {fileName} extracted" : $"Last error: {fileName}";
                this.SetTrayStatus(result.Message, result.Success ? ToolTipIcon.Info : ToolTipIcon.Error);
            }, null);
        }

        // Close/stop the program
        public void Close()
        {
            this.folderWatcher.Dispose();
            this.trayIcon.Dispose();
            Application.Exit();
        }

       /// <summary>
       /// Event handler that is called when the application exits. Used to cleanly dispose disposables
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
        private void OnApplicationExit(object sender, EventArgs e)
        {
            if (this.folderWatcher != null)
            {
                this.folderWatcher.Dispose();
            }
        }

        /// <summary>
        /// Initialize the system tray right-click menu
        /// </summary>
        private void InitializeContextMenu()
        {
            //Create menu
            this.menu = new ContextMenuStrip();
            this.menu.Opening += new CancelEventHandler(this.menu_Opening);
            this.menu.Renderer = new ModernMenuRenderer();
            this.menu.ShowImageMargin = true;
            this.menu.SuspendLayout();

            //Settings button
            this.menuItemSettings = new ToolStripMenuItem();
            this.menuItemSettings.Name = "menuItemSettings";
            this.menuItemSettings.Text = "Settings";
            this.menuItemSettings.Image = CreateMenuIcon(SystemIcons.Information, Color.FromArgb(70, 110, 180));
            this.menuItemSettings.Click += (sender, e) => this.ShowSettings();

            //Open watched folder button
            this.menuItemOpenFolder = new ToolStripMenuItem();
            this.menuItemOpenFolder.Name = "menuItemOpenFolder";
            this.menuItemOpenFolder.Text = "Open Watched Folder";
            this.menuItemOpenFolder.Image = CreateMenuIcon(SystemIcons.WinLogo, Color.FromArgb(230, 175, 45));
            this.menuItemOpenFolder.Click += (sender, e) => this.OpenWatchedFolder();

            //Last result button
            this.menuItemLastResult = new ToolStripMenuItem();
            this.menuItemLastResult.Name = "menuItemLastResult";
            this.menuItemLastResult.Text = this.lastResultText;
            this.menuItemLastResult.Enabled = false;
            this.menuItemLastResult.Image = CreateMenuIcon(SystemIcons.Asterisk, Color.FromArgb(90, 90, 90));

            //Exit button
            this.menuItemExit = new ToolStripMenuItem();
            this.menuItemExit.Name = "menuItemExit";
            this.menuItemExit.Text = "Exit";
            this.menuItemExit.Image = CreateMenuIcon(SystemIcons.Error, Color.FromArgb(190, 65, 65));
            this.menuItemExit.Click += (sender, e) => this.Close();

            //Running/Stopped button
            this.menuItemToggleRunning = new ToolStripMenuItem();
            this.menuItemToggleRunning.Name = "menuItemToggleRunning";
            this.menuItemToggleRunning.Click += new EventHandler(this.menuItemToggleRunning_Click);
            if (this.folderWatcher.IsRunning())
            {
                this.menuItemToggleRunning.Text = "Stop";
                this.menuItemToggleRunning.Image = CreateMenuIcon(SystemIcons.Shield, Color.FromArgb(220, 70, 70));
            }
            else
            {
                this.menuItemToggleRunning.Text = "Start";
                this.menuItemToggleRunning.Image = CreateMenuIcon(SystemIcons.Application, Color.FromArgb(60, 145, 90));
            }

            //Add the menu items to the menu
            this.menu.Items.AddRange(new ToolStripItem[]
            {
                this.menuItemToggleRunning,
                new ToolStripSeparator(),
                this.menuItemSettings,
                this.menuItemOpenFolder,
                new ToolStripSeparator(),
                this.menuItemLastResult,
                new ToolStripSeparator(),
                this.menuItemExit
            });

            this.menu.ResumeLayout(false);
            this.trayIcon.ContextMenuStrip = this.menu;

        }

        private void OpenWatchedFolder()
        {
            if (!System.IO.Directory.Exists(this.targetDirectory))
            {
                this.SetTrayStatus("Target folder does not exist.", ToolTipIcon.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = this.targetDirectory,
                UseShellExecute = true
            });
        }

        private void SetTrayStatus(string message, ToolTipIcon icon)
        {
            const int maxTextLength = 63;
            this.trayIcon.Text = message.Length > maxTextLength ? message.Substring(0, maxTextLength) : message;
            if (!Settings.Default.NotificationsEnabled)
            {
                return;
            }

            this.trayIcon.BalloonTipTitle = "Auto Unzipper";
            this.trayIcon.BalloonTipText = message;
            this.trayIcon.BalloonTipIcon = icon;
            this.trayIcon.ShowBalloonTip(3000);
        }

        private static Bitmap CreateMenuIcon(Icon icon, Color accent)
        {
            Bitmap bitmap = new Bitmap(20, 20);
            using Graphics graphics = Graphics.FromImage(bitmap);
            using SolidBrush background = new SolidBrush(Color.FromArgb(28, accent));
            graphics.Clear(Color.Transparent);
            graphics.FillEllipse(background, 1, 1, 18, 18);
            graphics.DrawIcon(icon, new Rectangle(3, 3, 14, 14));
            return bitmap;
        }

        private sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernMenuRenderer()
                : base(new ModernMenuColors())
            {
                this.RoundedEdges = true;
            }
        }

        private sealed class ModernMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(248, 249, 251);
            public override Color ImageMarginGradientBegin => Color.FromArgb(248, 249, 251);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(248, 249, 251);
            public override Color ImageMarginGradientEnd => Color.FromArgb(248, 249, 251);
            public override Color MenuItemSelected => Color.FromArgb(232, 240, 254);
            public override Color MenuItemBorder => Color.FromArgb(170, 195, 245);
            public override Color SeparatorDark => Color.FromArgb(222, 226, 232);
            public override Color SeparatorLight => Color.White;
        }

    }
}
