using System;
using System.IO;
using System.Security.Cryptography;

namespace GHelper.Security
{
    internal static class PluginVerifier
    {
        // SHA256 hash of the official PluginAdvancedSettings.zip
        internal const string PluginAdvancedSettingsHash = "b4a85e23e06c9c4d80cf538398015c89c8fb04453ed23784d7efe592dd4876f5";

        internal static bool Verify(string filePath)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
                return hash == PluginAdvancedSettingsHash;
            }
            catch
            {
                return false;
            }
        }
    }
}
