using GHelper.Helpers;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GHelper.AutoUpdate
{
    public class AutoUpdateControl
    {

        SettingsForm settings;

        public string versionUrl = "https://github.com/seerge/g-helper/releases";
        static long lastUpdate;

        public AutoUpdateControl(SettingsForm settingsForm)
        {
            settings = settingsForm;
            var appVersion = new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString());
            settings.SetVersionLabel(Properties.Strings.VersionLabel + $": {appVersion.Major}.{appVersion.Minor}.{appVersion.Build}");
        }

        public void CheckForUpdates()
        {
            // Run update once per 12 hours
            if (Math.Abs(DateTimeOffset.Now.ToUnixTimeSeconds() - lastUpdate) < 43200) return;
            lastUpdate = DateTimeOffset.Now.ToUnixTimeSeconds();

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                CheckForUpdatesAsync();
            });
        }

        public void LoadReleases()
        {
            try
            {
                Process.Start(new ProcessStartInfo(versionUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to open releases page:" + ex.Message);
            }
        }

        async void CheckForUpdatesAsync()
        {

            if (AppConfig.Is("skip_updates")) return;

            try
            {

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
                    var json = await httpClient.GetStringAsync("https://api.github.com/repos/seerge/g-helper/releases/latest");
                    var config = JsonSerializer.Deserialize<JsonElement>(json);
                    var tag = config.GetProperty("tag_name").ToString().Replace("v", "");
                    var assets = config.GetProperty("assets");

                    string url = null;
                    string hashUrl = null;

                    for (int i = 0; i < assets.GetArrayLength(); i++)
                    {
                        var downloadUrl = assets[i].GetProperty("browser_download_url").ToString();
                        if (downloadUrl.Contains(".zip"))
                            url = downloadUrl;
                        if (downloadUrl.Contains(".sha256"))
                            hashUrl = downloadUrl;
                    }

                    if (url is null)
                        url = assets[0].GetProperty("browser_download_url").ToString();

                    var gitVersion = new Version(tag);
                    var appVersion = new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    //appVersion = new Version("0.50.0.0"); 

                    if (gitVersion.CompareTo(appVersion) > 0)
                    {
                        versionUrl = url;
                        settings.SetVersionLabel(Properties.Strings.DownloadUpdate + ": " + tag, true);

                        string[] args = Environment.GetCommandLineArgs();
                        if (args.Length > 1 && args[1] == "autoupdate")
                        {
                            if (hashUrl != null)
                                await AutoUpdate(url, hashUrl);
                            return;
                        }

                        if (AppConfig.GetString("skip_version") != tag)
                        {
                            DialogResult dialogResult = MessageBox.Show(Properties.Strings.DownloadUpdate + ": G-Helper " + tag + "?", "Update", MessageBoxButtons.YesNo);
                            if (dialogResult == DialogResult.Yes)
                            {
                                if (hashUrl != null)
                                    await AutoUpdate(url, hashUrl);
                            }
                            else
                                AppConfig.Set("skip_version", tag);
                        }

                    }
                    else
                    {
                        Logger.WriteLine($"Latest version {appVersion}");
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to check for updates:" + ex.Message);
            }

        }

        public static string EscapeString(string input)
        {
            return Regex.Replace(Regex.Replace(input, @"\[|\]", "`$0"), @"\'", "''");
        }

        public static async Task<bool> AutoUpdate(string requestUri, string hashUri, HttpClient? httpClient = null)
        {
            Uri uri = new Uri(requestUri);
            Uri hash = new Uri(hashUri);

            if (uri.Scheme != Uri.UriSchemeHttps || hash.Scheme != Uri.UriSchemeHttps)
            {
                Logger.WriteLine("Update aborted: non-HTTPS URL provided.");
                return false;
            }

            string zipName = Path.GetFileName(uri.LocalPath);

            string exeLocation = Application.ExecutablePath;
            string exeDir = Path.GetDirectoryName(exeLocation);
            string exeName = Path.GetFileName(exeLocation);
            string zipLocation = Path.Combine(exeDir!, zipName);

            try
            {
                using (var client = httpClient ?? new HttpClient())
                {
                    var data = await client.GetByteArrayAsync(uri);
                    await File.WriteAllBytesAsync(zipLocation, data);

                    var expectedHash = (await client.GetStringAsync(hash)).Trim();

                    if (!VerifyFileHash(zipLocation, expectedHash))
                    {
                        Logger.WriteLine("Hash mismatch for downloaded update.");
                        if (OperatingSystem.IsWindows())
                            MessageBox.Show("Downloaded update failed integrity check and will not be installed.");
                        File.Delete(zipLocation);
                        return false;
                    }
                }

                Logger.WriteLine(requestUri);
                Logger.WriteLine(exeDir);
                Logger.WriteLine(zipName);
                Logger.WriteLine(exeName);

                string command =
                    $"$ErrorActionPreference = \"Stop\"; Set-Location -Path '{EscapeString(exeDir)}'; Wait-Process -Name \"GHelper\"; Expand-Archive \"{zipName}\" -DestinationPath . -Force; Remove-Item \"{zipName}\" -Force; \".\\{exeName}\"; ";
                Logger.WriteLine(command);

                try
                {
                    var cmd = new Process();
                    cmd.StartInfo.WorkingDirectory = exeDir;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.FileName = "powershell";
                    cmd.StartInfo.Arguments = command;
                    if (ProcessHelper.IsUserAdministrator()) cmd.StartInfo.Verb = "runas";
                    cmd.Start();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.Message);
                }

                Application.Exit();
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
                return false;
            }
        }

        public static bool VerifyFileHash(string filePath, string expectedHash)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
            return actualHash.Equals(expectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal);
        }

    }
}
