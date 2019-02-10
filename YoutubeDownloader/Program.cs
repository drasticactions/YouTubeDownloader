using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Models.ClosedCaptions;
using YoutubeExplode.Models.MediaStreams;

namespace YoutubeDownloader
{
    class Program
    {
        static YoutubeClient Client = new YoutubeClient();
        static YoutubeConverter Converter;
        static Progress<double> ProgressBar = new Progress<double>();
        static async Task Main(string username = "",
            string channelId = "",
            string playlistId = "",
            string directory = "", string ffmpegPath = "", bool audio = false, bool youtubeMusic = false, bool captions = false, int maxPage = 0)
        {
            if (string.IsNullOrEmpty(ffmpegPath))
                Converter = new YoutubeConverter(Client);
            else
                Converter = new YoutubeConverter(Client, ffmpegPath);

            ProgressBar.ProgressChanged += ProgressBar_ProgressChanged;


            if (captions)
                await GetCaptionsAsync(directory, maxPage, username, channelId, playlistId);
            else if (!string.IsNullOrEmpty(playlistId))
                await GetPlaylistAsync(directory, maxPage, playlistId, audio, youtubeMusic);
            else
                await GetChannelAsync(directory, maxPage, username, channelId, audio, youtubeMusic);
        }

        private static void ProgressBar_ProgressChanged(object sender, double e)
        {
            Console.Write($"\rDownloaded: {(int)(e * 100)}%");
        }

        private static async Task GetCaptionsAsync(string directory, int maxPage, string username = "", string channelId = "", string playlistId = "")
        {
            List<YoutubeExplode.Models.Video> videos = new List<YoutubeExplode.Models.Video>();
            var captionsId = "";
            if (!string.IsNullOrEmpty(playlistId))
            {
                var playlist = await Client.GetPlaylistAsync(playlistId);
                videos.AddRange(playlist.Videos);
                captionsId = playlist.Id;
            }
            else if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(channelId)) {
                if (!string.IsNullOrEmpty(username))
                    channelId = await Client.GetChannelIdAsync(username);
                System.Collections.Generic.IReadOnlyList<YoutubeExplode.Models.Video> channelUploads;
                if (maxPage > 0)
                    channelUploads = await Client.GetChannelUploadsAsync(channelId, maxPage);
                else
                    channelUploads = await Client.GetChannelUploadsAsync(channelId);
                videos.AddRange(channelUploads);
                captionsId = channelId;
            }

            List<CaptionVideo> captionVideos = new List<CaptionVideo>();
            for (int i = 0; i < videos.Count; i++)
            {
                YoutubeExplode.Models.Video upload = videos[i];
                var trackInfos = await Client.GetVideoClosedCaptionTrackInfosAsync(upload.Id);
                var captionVid = new CaptionVideo() { VideoId = 
                    upload.Id, VideoTitle = upload.Title,
                    UploadDate = upload.UploadDate,
                    VideoDuration = upload.Duration,
                    DislikeCount = upload.Statistics.DislikeCount,
                    LikeCount = upload.Statistics.LikeCount,
                    AverageRating = upload.Statistics.AverageRating,
                    ViewCount = upload.Statistics.ViewCount,
                };
                Console.WriteLine($"{i}: {upload.Title} ({upload.Id})");
                foreach (var trackInfo in trackInfos)
                {
                    Console.WriteLine($"{trackInfo.Language}");
                    var track = await Client.GetClosedCaptionTrackAsync(trackInfo);
                    captionVid.Tracks.Add(track);
                }

                captionVideos.Add(captionVid);
            }
            Console.WriteLine($"Total Videos: {videos.Count}");
            Console.WriteLine($"Captions Missing: {videos.Count - captionVideos.Count}");
            File.WriteAllText($"{captionsId}-captions.txt", JsonConvert.SerializeObject(captionVideos, Formatting.Indented));
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

        public class CaptionVideo
        {
            public string VideoId { get; set; }

            public string VideoTitle { get; set; }

            public DateTimeOffset UploadDate { get; set; }

            public TimeSpan VideoDuration { get; set; }

            public List<ClosedCaptionTrack> Tracks { get; set; } = new List<ClosedCaptionTrack>();
            public long DislikeCount { get; internal set; }
            public long LikeCount { get; internal set; }
            public double AverageRating { get; internal set; }
            public long ViewCount { get; internal set; }
        }
    }
}
