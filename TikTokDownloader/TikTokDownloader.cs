using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace TikTokDownloaderLib
{
    public class TikTokDownloader
    {
        private const string JsonScriptXPath = "//script[@id='__UNIVERSAL_DATA_FOR_REHYDRATION__']";
        private readonly HttpClient _client;

        public TikTokDownloader()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
            };

            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public async Task DownloadAllAsync(string tiktokUrl, string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            string html = await _client.GetStringAsync(tiktokUrl);
            string json = ExtractJsonFromHtml(html);

            string videoUrl = ExtractVideoUrl(json);
            string musicUrl = ExtractMusicUrl(json);

            if (!string.IsNullOrEmpty(videoUrl))
            {
                var videoData = await _client.GetByteArrayAsync(videoUrl);
                string videoPath = Path.Combine(outputFolder, "video.mp4");
                await File.WriteAllBytesAsync(videoPath, videoData);
                Console.WriteLine("Video downloaded");
            }
            else
            {
                Console.WriteLine("No video URL found");
            }

            if (!string.IsNullOrEmpty(musicUrl))
            {
                var musicData = await _client.GetByteArrayAsync(musicUrl);
                string musicPath = Path.Combine(outputFolder, "music.mp3");
                await File.WriteAllBytesAsync(musicPath, musicData);
                Console.WriteLine("Music downloaded");
            }
            else
            {
                Console.WriteLine("No music URL found");
            }
        }

        private string ExtractJsonFromHtml(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);
            var node = document.DocumentNode.SelectSingleNode(JsonScriptXPath);
            return node?.InnerText;
        }

        private string ExtractVideoUrl(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                return node?["__DEFAULT_SCOPE__"]?["webapp.video-detail"]?["itemInfo"]?["itemStruct"]?["video"]?["playAddr"]?.GetValue<string>();
            }
            catch { return null; }
        }

        private string ExtractMusicUrl(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                return node?["__DEFAULT_SCOPE__"]?["webapp.video-detail"]?["itemInfo"]?["itemStruct"]?["music"]?["playUrl"]?.GetValue<string>();
            }
            catch { return null; }
        }
    }
}
