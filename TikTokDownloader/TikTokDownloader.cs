using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using TagLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOFile = System.IO.File;
using TagFile = TagLib.File;

namespace TikTokDownloaderLib
{
    public class TikTokDownloader
    {
        const string JsonScriptXPath = "//script[@id='__UNIVERSAL_DATA_FOR_REHYDRATION__']";
        private readonly HttpClient client;

        public TikTokDownloader()
        {
            var cookies = new CookieContainer();

            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookies,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            };

            client = new HttpClient(handler);

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Referer", "https://www.tiktok.com/");

            Task.Run(() => EnsureScriptsExistAsync()).Wait();
        }

        public async Task DownloadVideoAsync(string url, string outputFolder, string fileName = "video.mp4")
        {
            Directory.CreateDirectory(outputFolder);

            string htmlJson = await GetHtmlFromTikTokUrlAsync(url);
            var jsonNode = JsonNode.Parse(htmlJson);
            string type = await GetTikTokTypeAsync(url);
            string apiUrl = await GenerateApiUrl(jsonNode);

            string videoUrl = await ExtractVideoUrl(apiUrl, type);

            if (!string.IsNullOrEmpty(videoUrl))
            {
                try
                {
                    var fileData = await client.GetByteArrayAsync(videoUrl);
                    string filePath = Path.Combine(outputFolder, fileName);
                    await IOFile.WriteAllBytesAsync(filePath, fileData);
                    Console.WriteLine($"Video saved as: {fileName}");
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
            else
            {
                Console.WriteLine("Failed to extract media URL.");
            }
        }

        public async Task DownloadImageAsync(string url, string outputFolder, string fileName = "photo.jpg")
        {
            Directory.CreateDirectory(outputFolder);

            string htmlJson = await GetHtmlFromTikTokUrlAsync(url);
            var jsonNode = JsonNode.Parse(htmlJson);
            string type = await GetTikTokTypeAsync(url);
            string apiUrl = await GenerateApiUrl(jsonNode);

            List<string> imageUrls = await ExtractImageUrls(apiUrl, type);

            if (imageUrls != null && imageUrls.Count > 0)
            {
                string extension = Path.GetExtension(fileName);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                for (int i = 0; i < imageUrls.Count; i++)
                {
                    try
                    {
                        var image = imageUrls[i];
                        var fileData = await client.GetByteArrayAsync(image);
                        string uniqueFileName = $"{nameWithoutExt}_{i}{extension}";
                        string filePath = Path.Combine(outputFolder, uniqueFileName);
                        await IOFile.WriteAllBytesAsync(filePath, fileData);
                        Console.WriteLine($"Image saved as: {uniqueFileName}");
                    }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
            }
            else
            {
                Console.WriteLine("Failed to extract media URL.");
            }
        }

        public async Task DownloadMusicAsync(string url, string outputFolder, string fileName = "music.mp3")
        {
            Directory.CreateDirectory(outputFolder);

            string htmlJson = await GetHtmlFromTikTokUrlAsync(url);
            var jsonNode = JsonNode.Parse(htmlJson);
            string type = await GetTikTokTypeAsync(url);
            string apiUrl = await GenerateApiUrl(jsonNode);
            var node = await GetJsonFromApi(apiUrl);

            var music = node["itemInfo"]?["itemStruct"]?["music"];
            string audioUrl = music?["playUrl"]?.GetValue<string>();
            string coverUrl = music?["coverLarge"]?.GetValue<string>();

            string title = music?["title"]?.GetValue<string>() ?? "TikTokMusic";
            string author = music?["authorName"]?.GetValue<string>() ?? "Unknown";

            if (type == "video" || type == "photo")
            {
                if (string.IsNullOrEmpty(audioUrl))
                {
                    Console.WriteLine("No audio found.");
                    return;
                }

                try
                {
                    byte[] fileData = await client.GetByteArrayAsync(audioUrl);
                    string filePath = Path.Combine(outputFolder, fileName);
                    await IOFile.WriteAllBytesAsync(filePath, fileData);

                    byte[] coverBytes = null;
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        coverBytes = await client.GetByteArrayAsync(coverUrl);
                    }

                    var file = TagFile.Create(filePath);
                    file.Tag.Title = title;
                    file.Tag.Performers = new[] { author };

                    if (coverBytes != null)
                    {
                        file.Tag.Pictures = new TagLib.IPicture[]
                        {
                    new TagLib.Picture(new TagLib.ByteVector(coverBytes))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        Description = "Cover",
                        MimeType = "image/jpeg"
                    }
                        };
                    }

                    file.Save();

                    Console.WriteLine($"Saved music with metadata: {fileName}");
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
            else
            {
                Console.WriteLine("Invalid link");
            }
        }

        public async Task<string> GetTikTokTypeAsync(string tiktokUrl)
        {
            var response = await client.GetAsync(tiktokUrl);
            Uri fullUrl = response.RequestMessage.RequestUri;
            var match = Regex.Match(fullUrl.ToString(), @"tiktok\.com/@[^/]+/(?<type>photo|video)/\d+");
            string type = match.Groups["type"].Value;
            return type;
        }

        public async Task<string> GetHtmlFromTikTokUrlAsync(string tiktokUrl)
        {
            string html = await client.GetStringAsync(tiktokUrl);
            return ExtractJsonFromHtml(html);
        }

        private string ExtractJsonFromHtml(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);
            var node = document.DocumentNode.SelectSingleNode(JsonScriptXPath);
            return node?.InnerText;
        }

        private static async Task<Dictionary<string, string>> GetQueryParamsAsync(JsonNode node, string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        {
            string browserName = userAgent.Split('/')[0];
            string browserVersion = userAgent.Contains('/') ? userAgent.Split('/')[1].Split(' ')[0] : "unknown";
            int historyLen = 0;
            string canonical = node?["__DEFAULT_SCOPE__"]?["seo.abtest"]?["canonical"]?.GetValue<string>();
            string lastSegment = canonical?.Split('/')?.Last();
            string itemId = lastSegment?.Split('?')?.First();
            var webIdLastTime = node?["__DEFAULT_SCOPE__"]?["webapp.app-context"]?["webIdCreatedTime"];
            string deviceId = node?["__DEFAULT_SCOPE__"]?["webapp.app-context"]?["wid"]?.GetValue<string>();
            string odinId = node?["__DEFAULT_SCOPE__"]?["webapp.app-context"]?["odinId"]?.GetValue<string>(); ;
            string abVersion = node?["__DEFAULT_SCOPE__"]?["webapp.app-context"]?["abTestVersion"]?["versionName"]?.GetValue<string>();
            List<int> abVersionList = abVersion?.Split(',')?.Select(x => int.TryParse(x, out var val) ? val : 0)?.ToList() ?? new List<int>();
            string clientABVersions = string.Join(",", abVersionList);

            var queryParams = new Dictionary<string, string>
            {
                { "WebIdLastTime", $"{webIdLastTime}"},
                { "aid", "1988" },
                { "app_language", "uk-UA" },
                { "app_name", "tiktok_web" },
                { "browser_language", "uk-UA" },
                { "browser_name", browserName },
                { "browser_online", "true" },
                { "browser_platform", "Win32" },
                { "browser_version", browserVersion },
                { "channel", "tiktok_web" },
                { "clientABVersions", clientABVersions},
                { "cookie_enabled", "true" },
                { "coverFormat", "2" },
                { "data_collection_enabled", "true" },
                { "device_id", deviceId},
                { "device_platform", "web_pc" },
                { "focus_state", "true" },
                { "from_page", "user" },
                { "history_len", $"{historyLen}"},
                { "is_fullscreen", "false" },
                { "is_page_visible", "true" },
                { "itemId", itemId },
                { "language", "uk-UA" },
                { "odinId", $"{odinId}"},
                { "os", "windows" },
                { "priority_region", "UA" },
                { "region", "UA" },
                { "screen_height", "1080" },
                { "screen_width", "1920" },
                { "tz_name", "Europe/Kiev" },
                { "user_is_login", "true" },
                { "webcast_language", "uk-UA" }
            };

            historyLen++;

            return queryParams;
        }

        private static async Task<string> GenerateApiUrl(JsonNode node, string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        {
            var queryParams = await GetQueryParamsAsync(node, userAgent);
            string query = string.Join("&", queryParams.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
            string xBogus = GenerateXBogus($"https://www.tiktok.com/api/item/detail/?{query}", userAgent);

            string url = $"https://www.tiktok.com/api/item/detail/?{query}&X-Bogus={xBogus}";
            return url;
        }

        private static string GenerateXBogus(string query, string userAgent)
        {
            string basePath = AppContext.BaseDirectory;
            string scriptPath = Path.Combine(basePath, "Scripts", "index.js");

            if (!IOFile.Exists(scriptPath))
                throw new FileNotFoundException($"Node.js script not found at: {scriptPath}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"\"{scriptPath}\" \"{Escape(query)}\" \"{Escape(userAgent)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string result = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(error))
                Console.WriteLine("[Node Error]: " + error);

            process.WaitForExit();

            return result;
        }

        private static string Escape(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private async Task<JsonNode> GetJsonFromApi(string url)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string jsonString = await response.Content.ReadAsStringAsync();

            var node = JsonNode.Parse(jsonString);
            return node;
        }

        private async Task<string> ExtractVideoUrl(string url, string type)
        {
            var node = await GetJsonFromApi(url);

            if (type == "video")
            {
                try
                {
                    var videos = node["itemInfo"]?["itemStruct"]?["video"]?["bitrateInfo"].AsArray();
                    var videoLast = videos.Last();
                    string videoUrl = videoLast["PlayAddr"]?["UrlList"][0].GetValue<string>();

                    return videoUrl;
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); return null; }
            }
            else
            {
                Console.WriteLine("InvalidLink");
                return null;
            }
        }

        private async Task<List<string>> ExtractImageUrls(string url, string type)
        {
            var node = await GetJsonFromApi(url);

            if (type == "photo")
            {
                try
                {
                    var images = node["itemInfo"]?["itemStruct"]?["imagePost"]?["images"].AsArray();
                    List<string> imagesUrls = new();

                    foreach (var image in images)
                    {
                        var image_url = image["imageURL"]?["urlList"][0].GetValue<string>();
                        if (!string.IsNullOrEmpty(image_url))
                        {
                            imagesUrls.Add(image_url);
                        }
                    }

                    return imagesUrls;
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); return null; }
            }
            else
            {
                Console.WriteLine("InvalidLink");
                return null;
            }
        }

        private static async Task EnsureScriptsExistAsync()
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "Scripts");
            Directory.CreateDirectory(folder);

            var filesToCheck = new (string FileName, string GitHubRawUrl)[]{
                ("index.js", "https://raw.githubusercontent.com/MrNaniii/TikTokDownloader/main/TikTokDownloader/Scripts/index.js"),
                ("xbogus.js", "https://raw.githubusercontent.com/MrNaniii/TikTokDownloader/main/TikTokDownloader/Scripts/xbogus.js")
            };

            using var httpClient = new HttpClient();

            foreach (var (fileName, rawUrl) in filesToCheck)
            {
                string localPath = Path.Combine(folder, fileName);
                bool shouldDownload = false;

                if (!IOFile.Exists(localPath))
                {
                    shouldDownload = true;
                }
                else
                {
                    try
                    {
                        var remote = await httpClient.GetStringAsync(rawUrl);
                        var local = await IOFile.ReadAllTextAsync(localPath);

                        if (!string.Equals(remote.Trim(), local.Trim(), StringComparison.Ordinal)) ;
                        {
                            shouldDownload = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking script {fileName}: {ex.Message}");
                        shouldDownload = true;
                    }
                }

                if (shouldDownload == true)
                {
                    try
                    {
                        string content = await httpClient.GetStringAsync(rawUrl);
                        await IOFile.WriteAllTextAsync(localPath, content);
                        Console.WriteLine($"Downloaded/Updated {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download {fileName}: {ex.Message}");
                    }
                }
            }
        }
    }
}