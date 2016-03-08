using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EZBlocker.Json;

namespace EZBlocker
{
    public class WebHelperHook
    {
        private readonly IWebRepository _webRepository;
        private OAuth _oauthToken;
        private CSRF _csrfToken;
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);

        public WebHelperHook(IWebRepository webRepository)
        {
            _webRepository = webRepository;
        }

        /**
         * Grabs the status of Spotify and returns a WebHelperResult object.
         **/
        public async Task<WebHelperResult> GetStatus()
        {
            await SetTokens();
            string url = GetUrl($"/remote/status.json?oauth={_oauthToken.t}&csrf={_csrfToken.token}");
            var spotifyAnswer = await _webRepository.GetData<SpotifyAnswer>(url);
            return WebHelperResult.CreateFromSpotifyResponse(spotifyAnswer);
        }

        private async Task SetTokens()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                if (_oauthToken == null)
                {
                    _oauthToken = await _webRepository.GetData<OAuth>("https://open.spotify.com/token");
                }
                if (_csrfToken == null)
                {
                    _csrfToken = await _webRepository.GetData<CSRF>(GetUrl("/simplecsrf/token.json"));
                }
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public static void CheckWebHelper()
        {
            if (Process.GetProcesses().Any(t => t.ProcessName.Equals("spotifywebhelper", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            try
            {
                var webHelper = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), @"\Spotify\SpotifyWebHelper.exe");
                Process.Start(webHelper);
                
            }
            catch
            {
                MessageBox.Show("Please check 'Allow Spotify to be started from the Web' in your Spotify preferences.", "EZBlocker");
            }
        }

        private static string GetUrl(string path)
        {
            return $"http://127.0.0.1:4380{path}";
        }

    }

}