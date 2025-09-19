using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Text;

namespace cas_cdrrestore_service.Helpers
{
    public class TarHelper
    {
        public void CreateSingleFileTar(string filePath, string outputPath, bool compress)
        {
            CreateTar([filePath], outputPath, compress);
        }

        public void CreateMultiFileTar(IEnumerable<string> filePaths, string outputPath, bool compress)
        {
            CreateTar(filePaths, outputPath, compress);
        }

        public void CreateTar(IEnumerable<string> filePaths, string outputPath, bool compress)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fsOut = File.Create(outputPath);
            Stream outStream = fsOut;

            if (compress)
                outStream = new GZipOutputStream(outStream);

            using var tarOut = new TarOutputStream(outStream, Encoding.UTF8)
            {
                IsStreamOwner = true
            };

            foreach (var filePath in filePaths)
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    continue;

                var entry = TarEntry.CreateTarEntry(fileInfo.Name);
                entry.Size = fileInfo.Length;
                tarOut.PutNextEntry(entry);

                using var fsIn = File.OpenRead(filePath);
                fsIn.CopyTo(tarOut);

                tarOut.CloseEntry();
            }

            tarOut.Close();

            if (compress && outStream is GZipOutputStream gz)
                gz.Finish();
        }

        public async Task<bool> ExtractSinglePcapAsync(Stream inputStream, bool inputGz, string callId, string outputFile, CancellationToken ct)
        {
            string expected = $"{callId}.pcap";

            var files = await ExtractMatchingEntriesAsync( inputStream, inputGz, outputDir: Path.GetDirectoryName(outputFile) ?? "", ct, nameOnly => string.Equals(nameOnly, expected, StringComparison.OrdinalIgnoreCase));

            return files.Any();
        }

        public async Task<List<string>> ExtractGraphFilesAsync(Stream inputStream, bool inputGz, string callId, string outputDir, CancellationToken ct)
        {
            return await ExtractMatchingEntriesAsync(inputStream, inputGz, outputDir, ct, nameOnly => nameOnly.StartsWith(callId, StringComparison.OrdinalIgnoreCase) && nameOnly.EndsWith(".graph", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<string>> ExtractMatchingEntriesAsync(Stream inputStream, bool inputGz, string outputDir, CancellationToken ct, Func<string, bool> match)
        {
            var extractedFiles = new List<string>();
            Stream inStream = inputGz ? new GZipInputStream(inputStream) : inputStream;

            using var tarIn = new TarInputStream(inStream, Encoding.UTF8);
            TarEntry? entry;

            while ((entry = tarIn.GetNextEntry()) != null)
            {
                if (ct.IsCancellationRequested) return extractedFiles;
                if (entry.IsDirectory) continue;

                string nameOnly = Path.GetFileName(entry.Name);
                if (!match(nameOnly)) continue;

                string relativePath = entry.Name.Replace("/", Path.DirectorySeparatorChar.ToString());
                string outputFile = Path.Combine(outputDir, relativePath);

                string? dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await using var fsOut = File.Create(outputFile);
                await CopyEntryContentsAsync(tarIn, fsOut, ct);
                extractedFiles.Add(outputFile);
            }

            return extractedFiles;
        }

        public async Task CopyEntryContentsAsync( TarInputStream tarIn, Stream outStream, CancellationToken ct, int bufferSize = 81920)
        {
            var buffer = new byte[bufferSize];
            int read;
            while ((read = tarIn.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (ct.IsCancellationRequested) return;
                await outStream.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }


    }
}
