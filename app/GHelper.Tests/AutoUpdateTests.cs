using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GHelper.AutoUpdate;
using Xunit;

namespace GHelper.Tests
{
    public class AutoUpdateTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<Uri, HttpResponseMessage> _responses;

            public FakeHttpMessageHandler(Dictionary<Uri, HttpResponseMessage> responses)
            {
                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_responses.TryGetValue(request.RequestUri!, out var response))
                    return Task.FromResult(response);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        [Fact]
        public async Task TamperedZipFailsVerification()
        {
            // Create original zip content
            byte[] originalZip;
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("test.txt");
                    await using var writer = new StreamWriter(entry.Open());
                    await writer.WriteAsync("hello");
                }
                originalZip = ms.ToArray();
            }

            // Compute hash of the original zip
            string hash;
            using (var sha256 = SHA256.Create())
            {
                hash = BitConverter.ToString(sha256.ComputeHash(originalZip)).Replace("-", string.Empty).ToLowerInvariant();
            }

            // Tamper with the zip content
            byte[] tamperedZip = (byte[])originalZip.Clone();
            tamperedZip[0] ^= 0xFF;

            var zipUri = new Uri("https://example.com/test.zip");
            var hashUri = new Uri("https://example.com/test.zip.sha256");

            var responses = new Dictionary<Uri, HttpResponseMessage>
            {
                { zipUri, new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tamperedZip) } },
                { hashUri, new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(hash) } }
            };

            var client = new HttpClient(new FakeHttpMessageHandler(responses));

            var result = await AutoUpdateControl.AutoUpdate(zipUri.ToString(), hashUri.ToString(), client);

            Assert.False(result, "Updater should reject tampered ZIP files.");
        }
    }
}
