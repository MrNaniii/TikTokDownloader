# TikTokDownloader

## NuGet
https://www.nuget.org/packages/TikTokDownloader/

## Download
```dotnet add package TikTokDownloader --version 1.0.8```

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
        await downloader.DownloadVideoAsync(
            "https://www.tiktok.com/@username/video/1234567890", 
            @"C:\Downloads\TikTok", 
            "video.mp4"
        );
        await downloader.DownloadMusicAsync(
            "https://www.tiktok.com/@username/video/1234567890", // or photo
            @"C:\Downloads\TikTok",
            "music.mp3"
        );
        await downloader.DownloadImageAsync(
            "https://www.tiktok.com/@username/photo/1234567890", 
            @"C:\Downloads\TikTok", 
            "photo.jpg"
        );
    }
}
```
