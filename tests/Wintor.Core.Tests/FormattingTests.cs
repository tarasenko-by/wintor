using System.Globalization;
using Wintor.Core;

namespace Wintor.Core.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(-5, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(15 * 1024 * 1024 + 300 * 1024, "15.3 MB")]
    [InlineData(500L * 1024 * 1024 * 1024, "500 GB")]
    [InlineData(3L * 1024 * 1024 * 1024 * 1024, "3 TB")]
    public void Bytes_UsesBinaryUnitsAndThreeSignificantDigits(long bytes, string expected)
    {
        Assert.Equal(expected, Formatting.Bytes(bytes, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Bytes_RespectsCultureDecimalSeparator()
    {
        Assert.Equal("1,5 KB", Formatting.Bytes(1536, CultureInfo.GetCultureInfo("ru-RU")));
    }

    [Fact]
    public void Rate_AppendsPerSecond()
    {
        Assert.Equal("2 MB/s", Formatting.Rate(2 * 1024 * 1024, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(12 * 60 + 30, "12m")]
    [InlineData(3 * 3600 + 5 * 60, "3h 05m")]
    [InlineData(2 * 86400 + 4 * 3600, "2d 4h")]
    public void Duration_IsCompact(int seconds, string expected)
    {
        Assert.Equal(expected, Formatting.Duration(TimeSpan.FromSeconds(seconds)));
    }
}
