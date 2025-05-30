# TikTokDownloader

## Usage

```csharp
using TikTokDownloaderLib;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var downloader = new TikTokDownloader();
        // Replace with your TikTok video URL and output folder path
        await downloader.DownloadAllAsync("https://www.tiktok.com/@username/video/1234567890", @"C:\Downloads\TikTok");
    }
}
```
