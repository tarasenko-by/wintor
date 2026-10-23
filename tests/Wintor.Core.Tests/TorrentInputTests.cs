using Wintor.Core;

namespace Wintor.Core.Tests;

public class TorrentInputTests
{
    [Theory]
    [InlineData("magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny")]
    [InlineData("  MAGNET:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c  ")]
    [InlineData("magnet:?xt=urn:btmh:1220caf1e1c30e81cb361b9ee167c4aa64228a7fa4fa9f6105232b28ad099f3a302e")]
    public void IsMagnetLink_AcceptsBitTorrentMagnets(string text)
    {
        Assert.True(TorrentInput.IsMagnetLink(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/file.torrent")]
    [InlineData("magnet:?xt=urn:ed2k:31d6cfe0d16ae931b73c59d7e0c089c0")]
    public void IsMagnetLink_RejectsEverythingElse(string? text)
    {
        Assert.False(TorrentInput.IsMagnetLink(text));
    }

    [Theory]
    [InlineData(@"C:\Downloads\ubuntu.torrent", true)]
    [InlineData(@"C:\Downloads\UBUNTU.TORRENT", true)]
    [InlineData(@"C:\Downloads\ubuntu.iso", false)]
    [InlineData(null, false)]
    public void IsTorrentFile_ChecksExtension(string? path, bool expected)
    {
        Assert.Equal(expected, TorrentInput.IsTorrentFile(path));
    }
}
