using Wintor.Core;

namespace Wintor.Core.Tests;

public class TorrentProblemTests
{
    [Fact]
    public void NoException_IsNoProblem()
    {
        Assert.Equal(TorrentProblem.None, TorrentProblems.Classify(null));
    }

    [Theory]
    [InlineData(unchecked((int)0x80070070), TorrentProblem.DiskFull)] // ERROR_DISK_FULL
    [InlineData(unchecked((int)0x80070027), TorrentProblem.DiskFull)] // ERROR_HANDLE_DISK_FULL
    [InlineData(unchecked((int)0x80070005), TorrentProblem.NoAccess)] // ERROR_ACCESS_DENIED
    [InlineData(unchecked((int)0x80070003), TorrentProblem.FilesMissing)] // ERROR_PATH_NOT_FOUND
    [InlineData(unchecked((int)0x80070020), TorrentProblem.Other)] // ERROR_SHARING_VIOLATION
    public void IOException_IsClassifiedByWin32Code(int hresult, TorrentProblem expected)
    {
        Assert.Equal(expected, TorrentProblems.Classify(new IOException("x", hresult)));
    }

    [Fact]
    public void WrappedExceptions_AreUnwrapped()
    {
        var wrapped = new InvalidOperationException("engine", new UnauthorizedAccessException());

        Assert.Equal(TorrentProblem.NoAccess, TorrentProblems.Classify(wrapped));
    }

    [Fact]
    public void MissingFolder_IsFilesMissing()
    {
        Assert.Equal(TorrentProblem.FilesMissing, TorrentProblems.Classify(new DirectoryNotFoundException()));
    }
}
