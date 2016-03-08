using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EZBlocker
{
    public class WebRepository : IWebRepository
    {
        private const string UserAgent = @"Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.111 Safari/537.36";

        public WebRepository()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public async Task<T> GetData<T>(string path)
        {
            string json = await GetData(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<string> GetData(string url)
        {
            using (WebClient client = new NonKeepAliveWebClient())
            {
                client.Headers.Add("user-agent", UserAgent);
                client.Headers.Add("Origin", "https://open.spotify.com");
                client.Encoding = Encoding.UTF8;
                var data = await client.DownloadStringTaskAsync(url);
                return data;
            }
        }
    }
}
