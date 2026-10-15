using FietsBridge.Core;

namespace FietsBridge.Tests;

public class DeviceAddressTests
{
    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        Assert.Equal("AA:BB:CC:DD:EE:FF", DeviceAddress.Format(0xAABBCCDDEEFF));
        Assert.Equal(0xAABBCCDDEEFFul, DeviceAddress.Parse("AA:BB:CC:DD:EE:FF"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB")]
    [InlineData("GG:BB:CC:DD:EE:FF")]
    public void Parse_Invalid_ReturnsNull(string text) => Assert.Null(DeviceAddress.Parse(text));
}
