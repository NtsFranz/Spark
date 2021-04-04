using System;

namespace Spark
{
	[Serializable]
	public class VersionJson
	{
		public string tag_name;
		public Author author;
		public string html_url;
		public Asset[] assets;
		public bool prerelease;
		public string body;
	}

	[Serializable]
	public class Asset
	{
		public string browser_download_url;
		public Uploader uploader;
	}

	[Serializable]
	public class Author
	{
		public string html_url;
	}

	[Serializable]
	public class Uploader
	{
		public string html_url;
	}
}