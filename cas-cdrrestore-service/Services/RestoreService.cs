using cas_cdrrestore_service.Helpers;
using cas_cdrrestore_service.Models;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace cas_cdrrestore_service.Services
{
    public class RestoreService
    {
        private readonly TarHelper _tarHelper;
        private readonly IHttpClientFactory _httpFactory;
        private readonly GoogleClient _googleClient;
        private readonly string _recordingsPcapBasePath;
        private readonly string _casPcapRecordingsGcsBucketName;
        private readonly int _maxDegreeOfParallelism;
        public RestoreService(TarHelper tarHelper, IHttpClientFactory httpFactory, GoogleClient googleClient, IConfiguration config)
        {
            _tarHelper = tarHelper;
            _httpFactory = httpFactory;
            _googleClient = googleClient;
            _recordingsPcapBasePath = config.GetValue<string>("RecordingsPcapBasePath")
               ?? throw new ArgumentNullException("RecordingsPcapBasePath is missing in configuration");
            _casPcapRecordingsGcsBucketName = config.GetValue<string>("CasPcapRecordingsGcsBucketName")
               ?? throw new ArgumentNullException("CasPcapRecordingsGcsBucketName is missing in configuration");
            _maxDegreeOfParallelism = config.GetValue<int?>("MaxDegreeOfParallelism") ?? 1;

        }
        public async Task<List<RestoreResponse>> RestoreManyAsync(List<RestoreRequest> cdrDetails)
        {
            var restoreResponseBag = new ConcurrentBag<RestoreResponse>();
            await Parallel.ForEachAsync(cdrDetails, new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism }, async (cdrDetail, cancellationToken) =>
            {
                var response = await RestoreSingleAsync(cdrDetail.CallDate, cdrDetail.CallId, cancellationToken);
                restoreResponseBag.Add( new RestoreResponse { CallId = cdrDetail.CallId, RestoreOutput = response });
            });
            return [.. restoreResponseBag];
        }
        public async Task<Dictionary<string, string>> RestoreSingleAsync(DateTime callDate, string callId, CancellationToken ct)
        {
            var result = new Dictionary<string, string>();

            try
            {
                var cdrRecordingFiles = GetCDRRecordingFilesPath(callDate);

                foreach (var (key, objectName) in cdrRecordingFiles)
                {
                    bool inputGz = key is "sip" or "graph";
                    bool compressOutput = inputGz;

                    var signedUrl = _googleClient.SignUrl(_casPcapRecordingsGcsBucketName, objectName, TimeSpan.FromHours(1));

                    await using var remoteStream = await DownloadStreamAsync(signedUrl, ct);
                    if (remoteStream is null)
                    {
                        result[key] = "not_found_on_gcs";
                        continue;
                    }

                    string tempFile = Path.Combine(_recordingsPcapBasePath, objectName.Replace("/", "\\"));
                    string tempDir = PrepareTempDir(tempFile);

                    string extractedFile = Path.Combine(tempDir, $"{callId}.pcap");
                    var graphFiles = new List<string>();
                    bool found;

                    if (key == "graph")
                    {
                        graphFiles = await _tarHelper.ExtractGraphFilesAsync(remoteStream, inputGz, callId, tempDir, ct);
                        found = graphFiles.Count > 0;
                    }
                    else
                    {
                        found = await _tarHelper.ExtractSinglePcapAsync(remoteStream, inputGz, callId, extractedFile, ct);
                    }

                    if (!found)
                    {
                        result[key] = "not_in_tar";
                        SafeDeleteDirectory(tempDir);
                        continue;
                    }

                    if (key == "graph")
                        _tarHelper.CreateMultiFileTar(graphFiles, tempFile, compressOutput);
                    else
                        _tarHelper.CreateSingleFileTar(extractedFile, tempFile, compressOutput);

                    SafeDeleteDirectory(tempDir);
                    result[key] = tempFile;
                }

                result["status"] = "done";
                return result;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, string>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message
                };
            }
        }

        private async Task<Stream?> DownloadStreamAsync(string signedUrl, CancellationToken ct)
        {
            using var client = _httpFactory.CreateClient("default");
            client.Timeout = TimeSpan.FromMinutes(5);

            var response = await client.GetAsync(signedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStreamAsync(ct);
        }

        private static string PrepareTempDir(string tempFile)
        {
            var dir = Path.GetDirectoryName(tempFile) ?? Path.GetTempPath();
            string tempDir = Path.Combine(dir, "Temp");
            if (string.IsNullOrEmpty(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            
            return tempDir;
        }

        private static void SafeDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
            }
        }

        public Dictionary<string, string> GetCDRRecordingFilesPath(DateTime timeStamp)
        {
            string date = $"{timeStamp:yyyy-MM-dd}";
            string hour = $"{timeStamp:HH}";
            string minute = $"{timeStamp:mm}";
            var datePath = $"{date}/{hour}/{minute}";

            return new Dictionary<string, string>
            {
                { "graph", $"{datePath}/GRAPH/graph_{date}-{hour}-{minute}.tar.gz" },
                { "rtp",   $"{datePath}/RTP/rtp_{date}-{hour}-{minute}.tar" },
                { "sip",   $"{datePath}/SIP/sip_{date}-{hour}-{minute}.tar.gz" }
            };
        }

    }
}
