using System;
using System.IO;
using Ryzen;
using Xunit;

public class OpenLibSysTests
{
    [Fact]
    public void BlocksUnsignedLibrary()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        Logger.appPath = tmpDir;
        Logger.logFile = Path.Combine(tmpDir, "log.txt");
        var dllPath = Path.Combine(tmpDir, "fake.dll");
        File.WriteAllText(dllPath, "not a real dll");

        using var ols = new Ols(dllPath);
        Assert.Equal((uint)Ols.Status.DLL_INVALID_SIGNATURE, ols.GetStatus());
    }
}
