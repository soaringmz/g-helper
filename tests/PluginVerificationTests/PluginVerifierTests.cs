using System;
using System.IO;
using System.IO.Compression;
using GHelper.Security;
using Xunit;

namespace PluginVerificationTests;

public class PluginVerifierTests
{
    [Fact]
    public void ModifiedZip_IsRejected()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "malicious");
        string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
        ZipFile.CreateFromDirectory(tempDir, tempZip);

        try
        {
            Assert.False(PluginVerifier.Verify(tempZip));
        }
        finally
        {
            File.Delete(tempZip);
            Directory.Delete(tempDir, true);
        }
    }
}
