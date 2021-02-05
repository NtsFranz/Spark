using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgniteBot
{
    [System.Serializable]
    public class VersionJson
    {
        public string tag_name;
        public Author author;
        public string html_url;
        public Asset[] assets;
    }
    [System.Serializable]
    public class Asset
    {
        public string browser_download_url;
        public Uploader uploader;
    }
    [System.Serializable]
    public class Author
    {
        public string html_url;
    }
    [System.Serializable]
    public class Uploader
    {
        public string html_url;
    }

}
