﻿using SpeedTest.Net.Enums;
using SpeedTest.Net.Helpers;
using SpeedTest.Net.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedTest.Net
{
    internal abstract class BaseHttpClient : HttpClient
    {
        public BaseHttpClient(HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler) { }
        public BaseHttpClient(HttpMessageHandler handler) : base(handler) { }
        public BaseHttpClient() : base() { }

        internal async Task<double> GetDownloadSpeed(IEnumerable<string> downloadUrls, int timeout = 5000)
        {
            var bytesPerSecond = 0D;

            bytesPerSecond += await GetDownloadedBytesPerSec(downloadUrls.First(), timeout);
            foreach (var url in downloadUrls.Skip(1))
            {
                bytesPerSecond += await GetDownloadedBytesPerSec(url, timeout);
                bytesPerSecond /= 2;
            }

            return bytesPerSecond;
        }

        internal async Task<double> GetUploadSpeed(string url, int timeout = 5000)
        {
            var bytesPerSecond = 0D;
            var uploadData = GenerateUploadData(2);

            bytesPerSecond += await GetUploadBytesPerSecond(url, uploadData.First(), timeout);
            foreach (var data in uploadData.Skip(1))
            {
                bytesPerSecond += await GetUploadBytesPerSecond(url, data, timeout);
                bytesPerSecond /= 2;
            }

            return bytesPerSecond;
        }

        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int MaxUploadSize = 4; // stop at 4k
        private static IEnumerable<byte[]> GenerateUploadData(int retryCount)
        {
            var random = new Random();
            var result = new List<byte[]>();

            for (var sizeCounter = 1; sizeCounter < MaxUploadSize + 1; sizeCounter++)
            {
                var size = sizeCounter * 200 * 1024;
                var data = new byte[size];

                for (var i = 0; i < size; ++i)
                {
                    data[i] = (byte)Chars[random.Next(Chars.Length)];
                }

                for (var i = 0; i < retryCount; i++)
                {
                    result.Add(data);
                }
            }

            return result;
        }

        private async Task<double> GetUploadBytesPerSecond(string uploadUrl, byte[] data, int timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            //cancellationTokenSource.CancelAfter(timeout);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            var sw = Stopwatch.StartNew();
            using (HttpResponseMessage response = await PostAsync(uploadUrl, new System.Net.Http.ByteArrayContent(data), cancellationToken))
            {
                sw.Stop();
                response.EnsureSuccessStatusCode();

                return data.Length / sw.Elapsed.TotalSeconds;
            }
        }

        internal async Task<double> GetLatancy(string url, int timeout = 5000)
        {
            var averagePing = 0D;
            for (var i = 0; i < 10; i++)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(timeout);
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                var sw = Stopwatch.StartNew();
                using (HttpResponseMessage response = await GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    sw.Stop();

                    response.EnsureSuccessStatusCode();
                }

                if (averagePing == 0D)
                {
                    averagePing = sw.ElapsedMilliseconds;
                }
                else
                {
                    averagePing += sw.ElapsedMilliseconds;
                    averagePing /= 2;
                }
            }
            return averagePing;
        }

        private async Task<double> GetDownloadedBytesPerSec(string downloadUrl, int timeout)
        {

            var totalRead = 0L;
            var buffer = new byte[8192];

            var sw = Stopwatch.StartNew();
            try
            {
                using (HttpResponseMessage response = await GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    cancellationTokenSource.CancelAfter(timeout);
                    CancellationToken cancellationToken = cancellationTokenSource.Token;

                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        var isMoreToRead = true;
                        do
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                totalRead += read;
                                //totalReads += 1;
                            }
                        }
                        while (isMoreToRead);
                        sw.Stop();
                    }
                }

            }
            catch
            {
                throw;
            }

            return totalRead / sw.Elapsed.TotalSeconds;
        }

        private static void DeleteFile(string tempFile)
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch { /**/ }
        }
    }
}