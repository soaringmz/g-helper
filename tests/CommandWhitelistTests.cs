using GHelper.Input;
using Xunit;

namespace GHelper.Tests;

public class CommandWhitelistTests
{
    [Fact]
    public void AllowsSimpleCommand()
    {
        Assert.True(CommandSanitizer.IsCommandAllowed("calc"));
    }

    [Fact]
    public void RejectsPipedCommand()
    {
        Assert.False(CommandSanitizer.IsCommandAllowed("calc && rm -rf /"));
    }
}
