# YouTubeDownloader

A simple .Net Core CLI program to download every video on a YouTube channel

Required Fields: 
--channel-id [YouTube Channel Id] 
OR
--username [YouTube Username]

Optional Fields: 
--directory [Output Directory For Videos] 
--ffmpeg-path [Path to FFMpeg] 
--max-page [Max Number of Video Pages to Download]

## Libraries used
- [YouTubeExplode](https://github.com/Tyrrrz/YoutubeExplode)
- [YoutubeExplode.Converter](https://github.com/Tyrrrz/YoutubeExplode.Converter)
