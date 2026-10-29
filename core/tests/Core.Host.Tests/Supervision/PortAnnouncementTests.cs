using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The core takes a port from one line the service writes and from nothing else.</summary>
public sealed class PortAnnouncementTests
{
    [Fact]
    public void ListeningLineGivesItsPort() =>
        Assert.Equal(54321, PortAnnouncement.PortIn(/*lang=json,strict*/ """{"event": "listening", "port": 54321}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("listening 54321")]
    [InlineData(/*lang=json,strict*/ """{"event": "started", "port": 54321}""")]
    [InlineData(/*lang=json,strict*/ """{"event": "listening"}""")]
    [InlineData(/*lang=json,strict*/ """{"event": "listening", "port": "54321"}""")]
    [InlineData(/*lang=json,strict*/ """{"event": "listening", "port": 0}""")]
    [InlineData(/*lang=json,strict*/ """{"event": "listening", "port": 65536}""")]
    [InlineData(/*lang=json,strict*/ """[54321]""")]
    public void AnythingElseGivesNoPort(string? line) => Assert.Null(PortAnnouncement.PortIn(line));
}
