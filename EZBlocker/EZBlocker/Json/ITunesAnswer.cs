﻿using System.Collections.Generic;

namespace EZBlocker
{
    public class Result
    {
        public int artistId;
        public string artistLinkUrl;
        public string artistName;
        public string artistType;
        public int primaryGenreId;
        public string primaryGenreName;
        public string radioStationUrl;
        public string wrapperType;
    }

    public class ITunesAnswer
    {
        public int resultCount;
        public IList<Result> results;
    }
}