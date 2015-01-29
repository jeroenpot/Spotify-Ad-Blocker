using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CoreAudio;
using EZBlocker.Properties;
using Newtonsoft.Json;

namespace EZBlocker
{
    public partial class MainWindow : Form
    {
        private const int WM_APPCOMMAND = 0x319;
        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int MEDIA_PLAYPAUSE = 0xE0000;

        private const string ua =
            @"Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.65 Safari/537.36";

        private const string website = @"http://www.ericzhang.me/projects/spotify-ad-blocker-ezblocker/";
        private readonly string _blocklistPath = Application.StartupPath + @"\blocklist.txt";
        private readonly string _coreaudioPath = Application.StartupPath + @"\CoreAudio.dll";
        private readonly string _jsonPath = Application.StartupPath + @"\Newtonsoft.Json.dll";
        private readonly string _nircmdPath = Application.StartupPath + @"\nircmd.exe";

        private readonly string EZBlockerUA = "EZBlocker " + Assembly.GetExecutingAssembly().GetName().Version + " " +
                                              Environment.OSVersion;

        // Google Analytics stuff
        private readonly Random rnd;
        private bool autoAdd;
        private string lastChecked = string.Empty; // Previous artist
        private long lasttime;
        private Dictionary<string, int> m_blockList;
        private bool muted;
        private bool notify;
        private int runs = 1;
        private bool spotifyMute;
        private string title = "Unset"; // Title of the Spotify window
        private float volume = 0.9f;

        public MainWindow()
        {
            CheckUpdate();

            if (!HasNet35())
                MessageBox.Show(".Net Framework 3.5 not found. EZBlocker may not work properly.", "EZBlocker");
            if (!File.Exists(_nircmdPath))
            {
                /*if (getOSArchitecture() == 64)
                    File.WriteAllBytes(nircmdPath, EZBlocker.Properties.Resources.nircmd64);
                else*/
                File.WriteAllBytes(_nircmdPath, Resources.nircmd32);
            }
            if (!File.Exists(_jsonPath))
            {
                File.WriteAllBytes(_jsonPath, Resources.Newtonsoft_Json);
            }
            if (!File.Exists(_coreaudioPath))
            {
                File.WriteAllBytes(_coreaudioPath, Resources.CoreAudio);
            }
            if (!File.Exists(_blocklistPath))
            {
                var w = new WebClient();
                w.Headers.Add("user-agent", EZBlockerUA);
                w.DownloadFile("http://www.ericzhang.me/dl/?file=blocklist.txt", _blocklistPath);
            }
            InitializeComponent();

            StartSpotify();

            ReadBlockList();

            rnd = new Random(Environment.TickCount);
            if (String.IsNullOrEmpty(Settings.Default.UID))
            {
                Settings.Default.UID = rnd.Next(100000000, 999999999).ToString(); // Build unique visitorId;
                Settings.Default.Save();
            }
            AutoAddCheckbox.Checked = Settings.Default.AutoAdd;
            NotifyCheckbox.Checked = Settings.Default.Notifications;
            SpotifyMuteCheckbox.Checked = Settings.Default.SpotifyMute;

            // minimize to tray
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        //public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);
        [DllImport("user32.dll", EntryPoint = "FindWindowEx")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private static void StartSpotify()
        {
            try
            {
                Process.Start(Environment.GetEnvironmentVariable("APPDATA") + @"\Spotify\spotify.exe");
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                    // Windows throttles down when minimized to task tray, so make sure EZBlocker runs smoothly
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Contains the logic for when to mute Spotify
         **/

        private void MainTimer_Tick(object sender, EventArgs e)
        {
            if (!UpdateTitle())
            {
            }
            if (!IsPlaying())
                return;
            var artist = GetArtist();
            if (lastChecked.Equals(artist))
                return;
            lastChecked = artist;
            if (autoAdd) // Auto add to block list
            {
                if (!IsInBlocklist(artist) && IsAd(artist))
                {
                    AddToBlockList(artist);
                    Notify("Automatically added " + artist + " to your blocklist.");
                }
            }
            if (IsInBlocklist(artist)) // Should mute
            {
                if (!muted)
                    Mute(1); // Mute Spotify
                ResumeTimer.Start();
            }
            else // Should unmute
            {
                if (muted)
                    Mute(0); // Unmute Spotify
                ResumeTimer.Stop();
                Notify(artist + " is not on your blocklist. Open EZBlocker to add it.");
            }
        }

        /**
         * Will attempt to play ad while muted
         **/

        private void ResumeTimer_Tick(object sender, EventArgs e)
        {
            UpdateTitle();
            if (!IsPlaying())
            {
                if (spotifyMute)
                {
                    SendMessage(GetHandle("SpotifyWindow"), WM_APPCOMMAND, Handle, (IntPtr) MEDIA_PLAYPAUSE);
                        // Play again   
                }
                else
                {
                    SendMessage(Handle, WM_APPCOMMAND, Handle, (IntPtr) MEDIA_PLAYPAUSE);
                }
            }
        }

        /**
         * Updates the title of the Spotify window.
         * 
         * Returns true if title updated successfully, false if otherwise
         **/

        private bool UpdateTitle()
        {
            foreach (var t in Process.GetProcesses().Where(t => t.ProcessName.Equals("spotify")))
            {
                title = t.MainWindowTitle;
                return true;
            }
            return false;
        }

        /**
         * Gets the Spotify process handle
         **/

        private IntPtr GetHandle(String classname)
        {
            foreach (var t in Process.GetProcesses().Where(t => t.ProcessName.Equals("spotify")))
            {
                return FindWindowEx(t.MainWindowHandle, new IntPtr(0), classname, null);
            }
            return IntPtr.Zero;
        }

        /**
         * Set's Spotify's process ID
         **/

        private bool SetProcessId()
        {
            foreach (var t in Process.GetProcesses().Where(t => t.ProcessName.Equals("spotify")))
            {
                return true;
            }
            return false;
        }

        /**
         * Determines whether or not Spotify is currently playing
         **/

        private bool IsPlaying()
        {
            return title.Contains("-");
        }

        /**
         * Returns the current artist
         **/

        private string GetArtist()
        {
            if (!IsPlaying()) return string.Empty;
            return title.Substring(10).Split('\u2013')[0].TrimEnd(); // Split at endash
        }

        /**
         * Adds an artist to the blocklist.
         * 
         * Returns false if Spotify is not playing.
         **/

        private bool AddToBlockList(string artist)
        {
            if (!IsPlaying() || IsInBlocklist(artist))
                return false;
            m_blockList.Add(artist, 0);
            File.AppendAllText(_blocklistPath, artist + "\r\n");
            return true;
        }

        private void ReadBlockList()
        {
            m_blockList =
                File.ReadAllLines(_blocklistPath)
                    .Distinct()
                    .Select((k, v) => new {Index = k, Value = v})
                    .ToDictionary(v => v.Index, v => v.Value);
        }

        /**
         * Mutes/Unmutes Spotify.
         
         * i: 0 = unmute, 1 = mute, 2 = toggle
         **/

        private void Mute(int i)
        {
            if (i > 2 || i < 0) return; //filter out invalid arguments
            if (i == 2) // Toggle mute
                i = (muted ? 0 : 1);
            muted = Convert.ToBoolean(i); //Or use Convert.ToBoolean if you'd prefer.
            var process = new Process(); // http://stackoverflow.com/questions/1469764/run-command-prompt-commands
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            if (spotifyMute) // Mute only Spotify process
            {
                // EZBlocker2.AudioUtilities.SetApplicationMute("spotify", muted);

                var DevEnum = new MMDeviceEnumerator();
                var device = DevEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                var asm = device.AudioSessionManager2;
                var sessions = asm.Sessions;
                for (var sid = 0; sid < sessions.Count; sid++)
                {
                    var id = sessions[sid].GetSessionIdentifier;
                    if (id.ToLower().IndexOf("spotify.exe") > -1)
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
                        //sessions[sid].SimpleAudioVolume.Mute = muted;
                    }
                }
            }
            else // Mute all of Windows
            {
                startInfo.Arguments = "/C nircmd mutesysvolume " + i;
                process.StartInfo = startInfo;
                process.Start();
            }
        }

        /**
         * Checks if an artist is in the blocklist (Exact match only)
         **/

        private bool IsInBlocklist(string artist)
        {
            return m_blockList.ContainsKey(artist);
        }

        /**
         * Attempts to check if the current song is an ad
         **/

        private bool IsAd(string artist)
        {
            try
            {
                var WebHelperResult = WebHelperHook.isAd();
                Console.WriteLine("WebHelperResult " + WebHelperResult);
                if (WebHelperResult > -1)
                {
                    if (WebHelperResult == 0)
                        return false;
                    return true;
                }
                return (isAdSpotify(artist) && IsAdiTunes(artist));
            }
            catch (Exception e)
            {
                Notify("Error occurred trying to connect to ad-detection servers.");
                try // Try again
                {
                    return (isAdSpotify(artist) && IsAdiTunes(artist));
                }
                catch
                {
                }
                return false;
            }
        }

        /**
         * Checks Spotify Web API to see if artist is an ad
         **/

        private bool isAdSpotify(String artist)
        {
            var url = "http://ws.spotify.com/search/1/artist.json?q=" + FormEncode(artist);
            var json = GetPage(url, ua);
            var res = JsonConvert.DeserializeObject<SpotifyAnswer>(json);
            foreach (var a in res.artists)
            {
                if (SimpleCompare(artist, a.name))
                    return false;
            }
            return true;
        }

        /**
         * Checks iTunes Web API to see if artist is an ad
         **/

        private bool IsAdiTunes(String artist)
        {
            var url = "http://itunes.apple.com/search?entity=musicArtist&limit=20&term=" + FormEncode(artist);
            var json = GetPage(url, ua);
            var res = JsonConvert.DeserializeObject<ITunesAnswer>(json);
            foreach (var r in res.results)
            {
                if (SimpleCompare(artist, r.artistName))
                    return false;
            }
            return true;
        }

        /**
         * Encodes an artist name to be compatible with web api's
         **/

        private string FormEncode(String param)
        {
            return param.Replace(" ", "+").Replace("&", "");
        }

        /**
         * Compares two strings based on lowercase alphanumeric letters and numbers only.
         **/

        private bool SimpleCompare(String a, String b)
        {
            var regex = new Regex("[^a-z0-9]");
            return String.Equals(regex.Replace(a.ToLower(), ""), regex.Replace(b.ToLower(), ""));
        }

        /**
         * Gets the source of a given URL
         **/

        private string GetPage(string URL, string ua)
        {
            var w = new WebClient();
            w.Headers.Add("user-agent", ua);
            var s = w.DownloadString(URL);
            return s;
        }

        private void Notify(String message)
        {
            if (notify)
                NotifyIcon.ShowBalloonTip(10000, "EZBlocker", message, ToolTipIcon.None);
        }

        /**
         * Checks if the current installation is the latest version. Prompts user if not.
         **/

        private void CheckUpdate()
        {
            if (Settings.Default.UpdateSettings) // If true, then first launch of latest EZBlocker
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                Settings.Default.Save();
                try
                {
                    File.Delete(_nircmdPath);
                    File.Delete(_jsonPath);
                }
                catch
                {
                }
            }
            try
            {
                var latest =
                    Convert.ToInt32(GetPage("http://www.ericzhang.me/dl/?file=EZBlocker-version.txt", EZBlockerUA));
                var current =
                    Convert.ToInt32(Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(".", ""));
                if (latest <= current)
                    return;
                if (
                    MessageBox.Show("There is a newer version of EZBlocker available. Would you like to upgrade?",
                        "EZBlocker", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start(website);
                    Application.Exit();
                }
            }
            catch
            {
                MessageBox.Show("Error checking for update.", "EZBlocker");
            }
        }

        /**
         * Send a request every 5 minutes to Google Analytics
         **/

        private void Heartbeat_Tick(object sender, EventArgs e)
        {
        }

        /**
         * Based off of: http://stackoverflow.com/questions/12851868/how-to-send-request-to-google-analytics-in-non-web-based-app
         * 
         * Logs actions using Google Analytics
         **/

        /**
         * http://andrewensley.com/2009/06/c-detect-windows-os-part-1/
         **/

        private bool HasNet35()
        {
            try
            {
                AppDomain.CurrentDomain.Load(
                    "System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /**
         * Processes window message and shows EZBlocker when attempting to launch a second instance.
         **/

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowUtilities.WM_SHOWAPP)
            {
                if (!ShowInTaskbar)
                {
                    WindowState = FormWindowState.Normal;
                    ShowInTaskbar = true;
                }
                else
                {
                    Activate();
                }
            }
            base.WndProc(ref m);
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!ShowInTaskbar)
            {
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
            }
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                //Notify("EZBlocker is hidden. Double-click this icon to restore.");
            }
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }

        private void BlockButton_Click(object sender, EventArgs e)
        {
            AddToBlockList(GetArtist());
            lastChecked = String.Empty; // Reset last checked so we can auto mute
        }

        private void AutoAddCheck_CheckedChanged(object sender, EventArgs e)
        {
            autoAdd = AutoAddCheckbox.Checked;
            Settings.Default.AutoAdd = autoAdd;
            Settings.Default.Save();
        }

        private void NotifyCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            notify = NotifyCheckbox.Checked;
            Settings.Default.Notifications = notify;
            Settings.Default.Save();
        }

        private void SpotifyMuteCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            spotifyMute = SpotifyMuteCheckbox.Checked;
            if (!spotifyMute) 
                MessageBox.Show("You may need to restart Spotify for this to take affect", "EZBlocker");
            Settings.Default.SpotifyMute = spotifyMute;
            Settings.Default.Save();
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            // Process.Start(blocklistPath);
            var bl = new Blocklist();
            bl.ShowDialog();
            ReadBlockList();
            lastChecked = String.Empty; // Reset last checked so we can auto mute
        }

        private void MuteButton_Click(object sender, EventArgs e)
        {
            Mute(2);
        }

        private void WebsiteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(website);
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Mute(0); // Unmute Spotify, if muted
        }
    }
}