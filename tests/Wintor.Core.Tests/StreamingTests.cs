using Wintor.Core;

namespace Wintor.Core.Tests;

public class HttpRangeTests
{
    [Theory]
    [InlineData(null, 0, 999)]
    [InlineData("bytes=0-", 0, 999)]
    [InlineData("bytes=100-199", 100, 199)]
    [InlineData("bytes=900-", 900, 999)]
    [InlineData("bytes=-100", 900, 999)]
    [InlineData("bytes=-5000", 0, 999)]
    [InlineData("bytes=500-5000", 500, 999)]
    [InlineData("bytes=0-0, 500-600", 0, 0)]
    [InlineData("items=0-10", 0, 999)]
    public void Parse_ReadsWhatPlayersSend(string? header, long start, long end)
    {
        Assert.Equal(new HttpRange(start, end), HttpRange.Parse(header, 1000));
    }

    [Theory]
    [InlineData("bytes=1000-")]
    [InlineData("bytes=200-100")]
    [InlineData("bytes=-0")]
    [InlineData("bytes=abc-")]
    public void Parse_RejectsRangesOutsideTheFile(string header)
    {
        Assert.Null(HttpRange.Parse(header, 1000));
    }

    [Fact]
    public void Length_CountsBothEnds()
    {
        Assert.Equal(100, new HttpRange(100, 199).Length);
    }
}

public class MediaFilesTests
{
    [Theory]
    [InlineData("Sintel/sintel-2010-4k.mkv", true)]
    [InlineData("bbb_sunflower_1080p_60fps_normal.MP4", true)]
    [InlineData("album/01 - track.flac", true)]
    [InlineData("ubuntu-24.04.1-desktop-amd64.iso", false)]
    [InlineData("subtitles/sintel_en.srt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPlayable_KnowsVideoAndAudio(string? path, bool playable)
    {
        Assert.Equal(playable, MediaFiles.IsPlayable(path));
    }

    [Fact]
    public void MainFile_IsTheLargestPlayableOne()
    {
        (int, string, long)[] files =
        [
            (0, "poster.jpg", 9_000_000),
            (1, "sample.mkv", 40_000_000),
            (2, "film.mkv", 4_000_000_000),
            (3, "film.srt", 100_000),
        ];

        Assert.Equal(2, MediaFiles.MainFile(files));
        Assert.Null(MediaFiles.MainFile([(0, "debian.iso", 1_000)]));
    }

    [Fact]
    public void ContentType_TellsThePlayerTheFormat()
    {
        Assert.Equal("video/x-matroska", MediaFiles.ContentType("a.mkv"));
        Assert.Equal("application/octet-stream", MediaFiles.ContentType("a.iso"));
    }
}

public class PlayerCommandTests
{
    private const string Url = "http://127.0.0.1:5000/token/film.mkv";

    [Theory]
    [InlineData("\"C:\\Program Files\\VideoLAN\\VLC\\vlc.exe\" --started-from-file \"%1\"",
        @"C:\Program Files\VideoLAN\VLC\vlc.exe", "--started-from-file \"" + Url + "\"")]
    [InlineData("\"C:\\mpv\\mpv.exe\" -- \"%L\"", @"C:\mpv\mpv.exe", "-- \"" + Url + "\"")]
    [InlineData("C:\\Program Files\\Player\\player.exe %1", @"C:\Program Files\Player\player.exe", "\"" + Url + "\"")]
    [InlineData("\"C:\\Players\\p.exe\" /play %1 %*", @"C:\Players\p.exe", "/play \"" + Url + "\"")]
    [InlineData("\"C:\\Players\\p.exe\"", @"C:\Players\p.exe", "\"" + Url + "\"")]
    public void Build_PutsTheLinkWhereTheFileGoes(string command, string program, string arguments)
    {
        Assert.Equal((program, arguments), PlayerCommand.Build(command, Url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rundll32.dll SomeHandler %1")]
    [InlineData("\"C:\\unterminated.exe")]
    public void Build_RefusesWhatIsNotAProgram(string? command)
    {
        Assert.Null(PlayerCommand.Build(command, Url));
    }

    [Fact]
    public void Build_ExpandsEnvironmentVariables()
    {
        var result = PlayerCommand.Build("\"%SystemRoot%\\player.exe\" \"%1\"", Url);

        Assert.Equal(Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "player.exe"), result?.Program);
    }
}
