using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using CoreAudio;
using System.Runtime.InteropServices;

namespace EZBlocker
{
    public partial class MainWindow : Form
    {
        private bool muted = false;
        private bool spotifyMute = false;
        private float volume = 0.9f;
        private string lastArtistName = "N/A";

        private readonly string nircmdPath = Application.StartupPath + @"\nircmd.exe";
        private readonly string jsonPath = Application.StartupPath + @"\Newtonsoft.Json.dll";
        private readonly string coreaudioPath = Application.StartupPath + @"\CoreAudio.dll";

        private readonly string spotifyPath = Environment.GetEnvironmentVariable("APPDATA") + @"\Spotify\spotify.exe";
        private readonly string volumeMixerPath = Environment.GetEnvironmentVariable("WINDIR") + @"\System32\SndVol.exe";

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646275%28v=vs.85%29.aspx
        private const int WM_APPCOMMAND = 0x319;
        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int MEDIA_PLAYPAUSE = 0xE0000;
        private const int MEDIA_NEXTTRACK = 0xB0000;

        private const string website = @"http://www.ericzhang.me/projects/spotify-ad-blocker-ezblocker/";

        private readonly WebHelperHook _webHelperHook;
        private readonly Timer _timer;

        public MainWindow()
        {
            InitializeComponent();

            // minimize to tray
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;

            _webHelperHook = new WebHelperHook(new WebRepository());
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += Processs;
            _timer.Start();
        }

        public async void Processs(object sender, EventArgs e)
        {
            try
            {
                WebHelperResult result = await _webHelperHook.GetStatus();
                SetTimer(result);
                HandleResult(result);
            }
            catch (Exception except)
            {
                StatusLabel.Text = "Connection Error";
                WebHelperHook.CheckWebHelper();
                Console.WriteLine(except);
            }
        }

        private void SetTimer(WebHelperResult webHelperResult)
        {
            _timer.Stop();
            _timer.Interval = webHelperResult.TimerInterval;
            _timer.Start();
        }

        private void HandleResult(WebHelperResult result)
        {
            if (result.IsAd) // Track is ad
            {
                if (result.IsPlaying)
                {
                    if (lastArtistName != result.DisplayLabel)
                    {
                        if (!muted)
                        {
                            Mute(1);
                        }
                        StatusLabel.Text = $"Muting ad: {ShortenName(result.DisplayLabel)}";
                        lastArtistName = result.DisplayLabel;
                    }
                }
                else // Ad is paused
                {
                    Resume();
                    StatusLabel.Text = $"Muting: {ShortenName(result.DisplayLabel)}";
                }
            }

            else if (!result.IsRunning)
            {
                StatusLabel.Text = "Spotify is not running";
                lastArtistName = "N/A";
            }
            else if (!result.IsPlaying)
            {
                StatusLabel.Text = "Spotify is paused";
                lastArtistName = "N/A";
            }
            else // Song is playing
            {
                if (muted)
                {
                    Mute(0);
                }

                if (lastArtistName != result.DisplayLabel)
                {
                    StatusLabel.Text = $"Playing: {ShortenName(result.DisplayLabel)}";
                    lastArtistName = result.DisplayLabel;
                }
            }
        }

        /**
         * Mutes/Unmutes Spotify.
         
         * i: 0 = unmute, 1 = mute, 2 = toggle
         **/
        private void Mute(int i)
        {
            if (i == 2) // Toggle mute
            {
                i = (muted ? 0 : 1);
            }

            muted = Convert.ToBoolean(i);

            if (spotifyMute) // Mute only Spotify process
            {
                MMDeviceEnumerator devEnum = new MMDeviceEnumerator();
                MMDevice device = devEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                AudioSessionManager2 asm = device.AudioSessionManager2;
                SessionCollection sessions = asm.Sessions;
                for (int sid = 0; sid < sessions.Count; sid++)
                {
                    string id = sessions[sid].GetSessionIdentifier;
                    if (id.IndexOf("spotify.exe", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        if (muted)
                        {
                            volume = sessions[sid].SimpleAudioVolume.MasterVolume;
                            sessions[sid].SimpleAudioVolume.MasterVolume = 0;
                        }
                        else
                        {
                            sessions[sid].SimpleAudioVolume.MasterVolume = volume;
                        }
                    }
                }
            }
            else // Mute all of Windows
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C nircmd mutesysvolume " + i.ToString();
                process.StartInfo = startInfo;
                process.Start();
            }

        }

        /**
         * Resumes playing Spotify
         **/
        private void Resume()
        {
            if (spotifyMute)
            {
                SendMessage(GetHandle(), WM_APPCOMMAND, this.Handle, (IntPtr)MEDIA_PLAYPAUSE);
            }
            else
            {
                SendMessage(this.Handle, WM_APPCOMMAND, this.Handle, (IntPtr)MEDIA_PLAYPAUSE);
            }
        }

        /**
         *  Plays next track queued on Spotify
         **/
        private void NextTrack()
        {
            if (spotifyMute)
            {
                SendMessage(GetHandle(), WM_APPCOMMAND, this.Handle, (IntPtr)MEDIA_NEXTTRACK);
            }
            else
            {
                SendMessage(this.Handle, WM_APPCOMMAND, this.Handle, (IntPtr)MEDIA_NEXTTRACK);
            }
        }

        /**
         * Gets the Spotify process handle
         **/
        private IntPtr GetHandle()
        {
            if (Process.GetProcesses().Any(t => t.ProcessName.ToLower().Contains("spotify")))
            {
                return FindWindow(null, "Spotify Free");
            }
            return IntPtr.Zero;
        }

        private string ShortenName(string name)
        {
            if (name.Length > 12)
            {
                return name.Substring(0, 12) + "...";
            }
            return name;
        }

        /**
         * Processes window message and shows EZBlocker when attempting to launch a second instance.
         **/
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowUtilities.WM_SHOWAPP)
            {
                if (!this.ShowInTaskbar)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.ShowInTaskbar = true;
                }
                else
                {
                    this.Activate();
                }
            }
            base.WndProc(ref m);
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!this.ShowInTaskbar)
            {
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            }
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;
            }
        }

        private void SpotifyMuteCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            spotifyMute = SpotifyMuteCheckbox.Checked;
            if (!spotifyMute)
            {
                MessageBox.Show("You may need to restart Spotify for this to take effect.", "EZBlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Properties.Settings.Default.SpotifyMute = spotifyMute;
            Properties.Settings.Default.Save();
        }

        private void VolumeMixerButton_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(volumeMixerPath);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not open Volume Mixer. This is only available on Windows 7/8/10", "EZBlocker");
            }
        }

        private void WebsiteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(website);
        }

        private void Main_Load(object sender, EventArgs e)
        {
            // Start Spotify and give EZBlocker higher priority
            try
            {
                if (File.Exists(spotifyPath))
                {
                    if (!FileVersionInfo.GetVersionInfo(spotifyPath).FileVersion.StartsWith("1."))
                    {
                        if (MessageBox.Show("You are using Spotify " + FileVersionInfo.GetVersionInfo(spotifyPath).FileVersion + ".\n\nPlease download EZBlocker v1.4.0.1 or upgrade to the newest Spotify to use EZBlocker v1.5.\n\nClick OK to continue to the EZBlocker website.", "EZBlocker", MessageBoxButtons.OKCancel) == DialogResult.OK)
                        {
                            Process.Start(website);
                            Application.Exit();
                        }
                    }
                    else
                    {
                        Process.Start(spotifyPath);
                    }
                }
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; // Windows throttles down when minimized to task tray, so make sure EZBlocker runs smoothly
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            // Extract dependencies
            if (!File.Exists(nircmdPath))
            {
                File.WriteAllBytes(nircmdPath, EZBlocker.Properties.Resources.nircmd32);
            }
            if (!File.Exists(jsonPath))
            {
                File.WriteAllBytes(jsonPath, EZBlocker.Properties.Resources.Newtonsoft_Json);
            }
            if (!File.Exists(coreaudioPath))
            {
                File.WriteAllBytes(coreaudioPath, EZBlocker.Properties.Resources.CoreAudio);
            }

            // Set up UI
            SpotifyMuteCheckbox.Checked = Properties.Settings.Default.SpotifyMute;
            Mute(0);
        }
    }
}
