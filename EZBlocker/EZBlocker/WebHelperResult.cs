using System;
using EZBlocker.Json;

namespace EZBlocker
{
    public class WebHelperResult
    {
        public bool IsRunning { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsAd { get; private set; }
        public int TimerInterval { get; set; }
        public string DisplayLabel { get; set; }

        public static WebHelperResult CreateFromSpotifyResponse(SpotifyAnswer answer)
        {
            WebHelperResult webHelperResult = new WebHelperResult();
            webHelperResult.IsPlaying = answer.playing;
            webHelperResult.IsRunning = answer.running;
            if (answer.playing && answer.running)
            {
                webHelperResult.TimerInterval = (int)((answer.track.length - answer.playing_position) * 1000) + 500;
            }
            else
            {
                webHelperResult.TimerInterval = 1000;
            }
            
            webHelperResult.DisplayLabel = GetLabel(answer.track);
            webHelperResult.IsAd = answer.track.track_type.Equals("ad", StringComparison.OrdinalIgnoreCase);
            return webHelperResult;
        }

        private static string GetLabel(Track track)
        {
            if (track?.track_resource == null || track?.artist_resource == null)
            {
                return null;
            }
            return $"{track?.track_resource?.name} ({track?.artist_resource?.name})";
        }
    }
}
