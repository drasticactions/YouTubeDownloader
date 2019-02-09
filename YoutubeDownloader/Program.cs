using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Models.MediaStreams;

namespace YoutubeDownloader
{
    class Program
    {
        static YoutubeClient Client = new YoutubeClient();
        static YoutubeConverter Converter;
        static Progress<double> ProgressBar = new Progress<double>();
        static async Task Main(string username = "", string channelId = "", string playlistId = "", string directory = "", string ffmpegPath = "", bool audio = false, bool youtubeMusic = false, int maxPage = 0)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
                Converter = new YoutubeConverter(Client);
            else
                Converter = new YoutubeConverter(Client, ffmpegPath);

            ProgressBar.ProgressChanged += ProgressBar_ProgressChanged;

            if (!string.IsNullOrEmpty(playlistId))
                await GetPlaylistAsync(directory, maxPage, playlistId, audio, youtubeMusic);
            else
                await GetChannelAsync(directory, maxPage, username, channelId, audio, youtubeMusic);
        }

        private static void ProgressBar_ProgressChanged(object sender, double e)
        {
            Console.Write($"\rDownloaded: {(int)(e * 100)}%");
        }

        private static async Task GetPlaylistAsync(string directory, int maxPage, string playlistId = "", bool audio = false, bool youtubeMusic = false)
        {
            var playlist = await Client.GetPlaylistAsync(playlistId);
            directory = string.IsNullOrEmpty(directory) ? playlist.Title : directory;
            Directory.CreateDirectory(directory);
            Console.WriteLine($"Playlist: {playlist.Title}");
            var playlistTitle = playlist.Title;
            if (youtubeMusic)
                playlistTitle = playlist.Title.Split('-').Last().Trim();

            for (int i = 0; i < playlist.Videos.Count; i++)
            {
                YoutubeExplode.Models.Video upload = playlist.Videos[i];
                Console.WriteLine($"{System.Environment.NewLine}Video {i + 1} of {playlist.Videos.Count}: {upload.Title}");
                await Download(upload, directory, audio, youtubeMusic, playlistTitle, i + 1, playlist.Videos.Count);
            }
        }

        private static async Task GetChannelAsync(string directory, int maxPage, string username = "", string channelId = "", bool audio = false, bool youtubeMusic = false)
        {
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

            for (int i = 0; i < channelUploads.Count; i++)
            {
                YoutubeExplode.Models.Video upload = channelUploads[i];
                Console.WriteLine($"{System.Environment.NewLine}Video {i + 1} of {channelUploads.Count}: {upload.Title}");
                await Download(upload, directory, audio, youtubeMusic);
            }
        }

        private static async Task Download(YoutubeExplode.Models.Video upload, string directory, bool audio, bool youtubeMusic, string playlistTitle = "", int playlistNum = 0, int playlistCount = 0)
        {
            var mediaStreamInfoSet = await Client.GetVideoMediaStreamInfosAsync(upload.Id);
            var audioStreamInfo = mediaStreamInfoSet.Audio.WithHighestBitrate();
            var videoStreamInfo = mediaStreamInfoSet.Video.WithHighestVideoQuality();
            if (!audio)
            {
                var mediaStreamInfos = new MediaStreamInfo[] { audioStreamInfo, videoStreamInfo };
                await Converter.DownloadAndProcessMediaStreamsAsync(mediaStreamInfos, $"{directory}\\{upload.Id}.mp4", "mp4", ProgressBar);
            }
            else
            {
                var mediaStreamInfos = new MediaStreamInfo[] { audioStreamInfo };
                await Converter.DownloadAndProcessMediaStreamsAsync(mediaStreamInfos, $"{directory}\\{upload.Id}.mp3", "mp3", ProgressBar);
                if (!youtubeMusic)
                    return;
                var tfile = TagLib.File.Create($"{directory}\\{upload.Id}.mp3");
                var artistSong = upload.Author.Split('-');
                tfile.Tag.AlbumArtists = new string[1] { artistSong[0] };
                tfile.Tag.Performers = new string[1] { artistSong[0] };
                tfile.Tag.Album = playlistTitle;
                tfile.Tag.Title = upload.Title;
                tfile.Tag.Track = (uint)playlistNum;
                tfile.Tag.TrackCount = (uint)playlistCount;
                tfile.Save();
                //tfile.Tag.Pictures = new TagLib.IPicture[1] { new TagLib.Picture() };
            }
        }
    }
}
