using System;
using System.IO;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;

namespace YoutubeDownloader
{
    class Program
    {
        static YoutubeClient Client = new YoutubeClient();
        static YoutubeConverter Converter;
        static Progress<double> ProgressBar = new Progress<double>();
        static async Task Main(string username = "", string channelId = "", string directory = "", string ffmpegPath = "", int maxPage = 0)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
                Converter = new YoutubeConverter(Client);
            else
                Converter = new YoutubeConverter(Client, ffmpegPath);

            ProgressBar.ProgressChanged += ProgressBar_ProgressChanged;

            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(channelId))
            {
                Console.WriteLine("Required Fields: --channel-id [YouTube Channel Id] OR --username [YouTube Username]");
                Console.WriteLine("Optional Fields: --directory [Output Directory For Videos] --ffmpeg-path [Path to FFMpeg] --max-page [Max Number of Video Pages to Download]");
                return;
            }

            if (string.IsNullOrEmpty(channelId))
                channelId = await Client.GetChannelIdAsync(username);

            if (string.IsNullOrEmpty(directory))
                directory = channelId;

            Directory.CreateDirectory(directory);
            var channel = await Client.GetChannelAsync(channelId);
            Console.WriteLine($"Channel: {channel.Title}");
            System.Collections.Generic.IReadOnlyList<YoutubeExplode.Models.Video> channelUploads;
            if (maxPage > 0)
                channelUploads = await Client.GetChannelUploadsAsync(channelId, maxPage);
            else
                channelUploads = await Client.GetChannelUploadsAsync(channelId);

            foreach (var upload in channelUploads)
            {
                Console.WriteLine($"Video: {upload.Title}");
                await Converter.DownloadVideoAsync(upload.Id, $"{directory}\\{upload.Id}.mp4", ProgressBar);
            }
        }

        private static void ProgressBar_ProgressChanged(object sender, double e)
        {
            Console.Write($"\rDownloaded: {(int)(e * 100)}%");
        }
    }
}
