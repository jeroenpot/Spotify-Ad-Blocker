namespace EZBlocker.Json
{
    public class SpotifyAnswer
    {
        public int version { get; set; }
        public string client_version { get; set; }
        public bool playing { get; set; }
        public bool shuffle { get; set; }
        public bool repeat { get; set; }
        public bool play_enabled { get; set; }
        public bool prev_enabled { get; set; }
        public bool next_enabled { get; set; }
        public Track track { get; set; }
        public Context context { get; set; }
        public float playing_position { get; set; }
        public int server_time { get; set; }
        public float volume { get; set; }
        public bool online { get; set; }
        public Open_Graph_State open_graph_state { get; set; }
        public bool running { get; set; }
    }

    public class Track
    {
        public Track_Resource track_resource { get; set; }
        public Artist_Resource artist_resource { get; set; }
        public Album_Resource album_resource { get; set; }
        public int length { get; set; }
        public string track_type { get; set; }
    }

    public class Track_Resource
    {
        public string name { get; set; }
        public string uri { get; set; }
        public Location location { get; set; }
    }

    public class Location
    {
        public string og { get; set; }
    }

    public class Artist_Resource
    {
        public string name { get; set; }
        public string uri { get; set; }
        public Location location { get; set; }
    }



    public class Album_Resource
    {
        public string name { get; set; }
        public string uri { get; set; }
        public Location location { get; set; }
    }

    public class Context
    {
    }

    public class Open_Graph_State
    {
        public bool private_session { get; set; }
        public bool posting_disabled { get; set; }
    }
}
