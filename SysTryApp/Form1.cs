namespace SysTryApp
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using System;
    using System.Drawing;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Diagnostics;
    using System.Text;
    using System.Drawing.Imaging;
    using LiteDB;
    using static System.Runtime.InteropServices.JavaScript.JSType;
    using static System.Windows.Forms.Design.AxImporter;
    using static System.Net.Mime.MediaTypeNames;
    using System.Timers;

    public partial class Form1 : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private NamedPipeServerStream _pipeServer;
        private NamedPipeClientStream _pipeClient;

        public LiteDatabase Database { get; set; }

        private Point lastMousePosition = Point.Empty;
        private DateTime lastMouseMoveTime = DateTime.MinValue;
        private Thread mouseTrackingThread;
        private volatile bool isTracking;
        private FileSystemWatcher watcher;
        public System.Timers.Timer _timer;
        public Form1()
        {
            //this.SuspendLayout();
            this.InitializeComponent();
            //this.ClientSize = new System.Drawing.Size(300, 200);
            this.Name = "SystemTrayApp";
            this.Text = "Command Exec";
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ResumeLayout(false);

            this.FormClosing += SystemTrayApp_FormClosing;
            string filePath = $@"{Constants.DataPath}\Files\litedb\litedb.db";
            string connectionString = $"Filename=\"{filePath}\";Connection=\"shared\"";
            Database = new LiteDatabase(connectionString);
            InitializeSystemTray();
            isTracking = true;
            mouseTrackingThread = new Thread(new ThreadStart(TrackMousePosition));
            mouseTrackingThread.IsBackground = true; // Set as background thread so it doesn't block the app from closing
            mouseTrackingThread.Start();

            string directory = $"{Constants.FileWatcher}";  // Make sure this directory exists
            watcher = new FileSystemWatcher();
            watcher.Path = $"{Constants.FileWatcher}\\";
            watcher.Filter = "ss.txt";
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.LastAccess;

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.EnableRaisingEvents = true;
            _timer = new System.Timers.Timer(6000); // 60,000 ms = 60 seconds
            _timer.Elapsed += UpdateMouse;  // Event handler for the timer
            _timer.AutoReset = true;  // Ensures the timer runs repeatedly
            _timer.Enabled = true;  // Start the timer
        }

        private void UpdateMouse(object? sender, ElapsedEventArgs e)
        {
            var col = Database.GetCollection<MouseData>("mouse");
            var jk = col.Query().ToList();
            if (col.Query().Count() == 0)
            {
                col.Insert(new MouseData { LastMouse = lastMouseMoveTime.ToString("dd-MM-yyyy HH:mm:ss") });
            }
            else
            {
                var prev = col.Query().First();
                prev.LastMouse = lastMouseMoveTime.ToString("dd-MM-yyyy HH:mm:ss");
                col.Update(prev);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Thread.Sleep(200);
            // Read the tx_id and create a file in the images folder
            string txIdFile = File.ReadAllText(e.FullPath);
            var screenshotBytes = GetScreenshot();
            File.WriteAllBytes("screenshot.png", screenshotBytes);
            string tvFramePath = "tvframe.png";
            Bitmap tvFrame = new Bitmap(tvFramePath);

            // Convert TV frame to a non-indexed format (e.g., 32bpp ARGB)
            Bitmap tvFrameNonIndexed = new Bitmap(tvFrame.Width, tvFrame.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(tvFrameNonIndexed))
            {
                g.DrawImage(tvFrame, 0, 0);
            }
            Byte[] data;

            // Load the screenshot from byte array
            using (MemoryStream ms = new MemoryStream(screenshotBytes))
            {
                Bitmap screenshot = new Bitmap(ms);

                // Define the TV screen area within the TV frame
                Rectangle tvScreenArea = new Rectangle(x: 60, y: 30, width: 800, height: 455);

                // Resize the screenshot to fit the TV screen area
                Bitmap resizedScreenshot = new Bitmap(tvScreenArea.Width, tvScreenArea.Height);
                using (Graphics g = Graphics.FromImage(resizedScreenshot))
                {
                    g.DrawImage(screenshot, 0, 0, tvScreenArea.Width, tvScreenArea.Height);
                }

                // Combine the resized screenshot with the TV frame
                using (Graphics g = Graphics.FromImage(tvFrameNonIndexed))
                {
                    g.DrawImage(resizedScreenshot, tvScreenArea.X, tvScreenArea.Y);
                }

                // Save the final image

                using (var memoryStream = new MemoryStream())
                {
                    tvFrameNonIndexed.Save(memoryStream, ImageFormat.Bmp);

                    data = memoryStream.ToArray();
                }
                tvFrameNonIndexed.Save($"{Constants.FileWatcher}\\{txIdFile}.png", ImageFormat.Png);
            }
        }

        private void InitializeSystemTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, OnShow);
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Command Exec";
            trayIcon.Icon = SystemIcons.Application; // You can set a custom icon here
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            // Handle double-click on tray icon
            trayIcon.DoubleClick += TrayIcon_DoubleClick;
        }

        private async void ProcessCommandAsync(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            if (command.Split('!')[0] == "SERVER") return;
            //trayIcon.ShowBalloonTip(3000, "Received", $"Command: {command}", ToolTipIcon.Info);
            if (command.Split('!')[1] == "getScreenshot")
            {
                var screenshotBytes = GetScreenshot();
                File.WriteAllBytes("screenshot.png", screenshotBytes);
                string tvFramePath = "tvframe.png";
                Bitmap tvFrame = new Bitmap(tvFramePath);

                // Convert TV frame to a non-indexed format (e.g., 32bpp ARGB)
                Bitmap tvFrameNonIndexed = new Bitmap(tvFrame.Width, tvFrame.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(tvFrameNonIndexed))
                {
                    g.DrawImage(tvFrame, 0, 0);
                }
                Byte[] data;

                // Load the screenshot from byte array
                using (MemoryStream ms = new MemoryStream(screenshotBytes))
                {
                    Bitmap screenshot = new Bitmap(ms);

                    // Define the TV screen area within the TV frame
                    Rectangle tvScreenArea = new Rectangle(x: 60, y: 30, width: 800, height: 455);

                    // Resize the screenshot to fit the TV screen area
                    Bitmap resizedScreenshot = new Bitmap(tvScreenArea.Width, tvScreenArea.Height);
                    using (Graphics g = Graphics.FromImage(resizedScreenshot))
                    {
                        g.DrawImage(screenshot, 0, 0, tvScreenArea.Width, tvScreenArea.Height);
                    }

                    // Combine the resized screenshot with the TV frame
                    using (Graphics g = Graphics.FromImage(tvFrameNonIndexed))
                    {
                        g.DrawImage(resizedScreenshot, tvScreenArea.X, tvScreenArea.Y);
                    }

                    // Save the final image

                    using (var memoryStream = new MemoryStream())
                    {
                        tvFrameNonIndexed.Save(memoryStream, ImageFormat.Bmp);

                        data = memoryStream.ToArray();
                    }
                }
                string base64Str = Convert.ToBase64String(data);
            }
            else if (command.Split('!')[1] == "getLastMouseMoved")
            {
                //messageService.SendMessage("STR!mouse!" + lastMouseMoveTime.ToString("dd-MM-yyyy HH:mm:ss"));
            }
            else
            {
                string output = await RunCommandAsync(command);

                //messageService.SendMessage($"STR!command!{output}");
            }
        }

        public static byte[] GetScreenshot()
        {
            // Get the bounds of the primary screen
            Rectangle bounds = Screen.PrimaryScreen.Bounds;

            // Create a new bitmap with the dimensions of the screen
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                // Create a graphics object from the bitmap
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // Capture the screenshot by copying from the screen
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }

                // Convert the bitmap to a byte array
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png); // Save the bitmap as PNG format
                    return ms.ToArray(); // Return the byte array
                }
            }
        }

        private async Task<string> RunCommandAsync(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c {command}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => output.AppendLine(e.Data);
                process.ErrorDataReceived += (sender, e) => error.AppendLine(e.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (error.Length > 0)
                {
                    output.AppendLine("Errors:");
                    output.Append(error);
                }

                return output.ToString();
            }
        }

        private void OnShow(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void OnExit(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop the tracking thread when the form is closing
            isTracking = false;
            if (mouseTrackingThread != null && mouseTrackingThread.IsAlive)
            {
                mouseTrackingThread.Join();
            }

            base.OnFormClosing(e);
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void SystemTrayApp_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
            base.OnResize(e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var pass = Database.GetCollection<Setting>("setting");
            pass.DeleteAll();
            pass.Insert(new Setting { Password = textBox1.Text });
        }

        private void TrackMousePosition()
        {
            while (isTracking)
            {
                Point currentMousePosition = Control.MousePosition;

                if (currentMousePosition != lastMousePosition)
                {
                    lastMousePosition = currentMousePosition;
                    lastMouseMoveTime = DateTime.Now; // Update the last move time                    
                }

                // Sleep for a short period to prevent excessive CPU usage
                Thread.Sleep(50);
            }
        }
    }
    public class Setting
    {
        public string Password { get; set; }
        public Guid ConnectedWifiID { get; set; }
    }
    public class Constants
    {
        public static string DataPath { get; set; } = "D:\\Db\\";
        public static string FileWatcher { get; set; } = "D:\\Db\\";
    }
}
