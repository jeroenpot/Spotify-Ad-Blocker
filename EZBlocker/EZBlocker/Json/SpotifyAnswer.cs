using System.Collections.Generic;

namespace EZBlocker
{
    public class Info
    {
        public int limit;
        public int num_results;
        public int offset;
        public int page;
        public string query;
        public string type;
    }

    public class Artist
    {
        public string href;
        public string name;
        public float popularity;
    }

    public class SpotifyAnswer
    {
        public IList<Artist> artists;
        public Info info;
    }
}