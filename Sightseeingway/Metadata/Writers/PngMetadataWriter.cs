using Newtonsoft.Json;
using Sightseeingway.Results;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;

namespace Sightseeingway.Metadata.Writers
{
    /// <summary>
    /// Hand-rolled PNG writer that splices a v1 metadata payload into the file
    /// as an iTXt chunk with keyword <c>Sightseeingway</c>. Streams the file
    /// rather than buffering it; bounded memory regardless of image size.
    ///
    /// Idempotent: any existing iTXt chunk with our keyword is dropped from
    /// the output, so re-injection replaces rather than appends.
    /// Atomic: writes to <c>&lt;target&gt;.tmp</c> and renames over the original.
    /// </summary>
    public sealed class PngMetadataWriter : IMetadataWriter
    {
        public const string Keyword = "Sightseeingway";
        private const int CopyBufferSize = 64 * 1024;

        private static readonly byte[] PngSignature =
        {
            0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A,
        };

        private static readonly byte[] IendType = { (byte)'I', (byte)'E', (byte)'N', (byte)'D' };
        private static readonly byte[] ITxtType = { (byte)'i', (byte)'T', (byte)'X', (byte)'t' };

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string Name => "png";

        public OperationResult Write(string filePath, StateSnapshot snapshot, CancellationToken ct)
        {
            var tmpPath = filePath + ".tmp";

            try
            {
                var payloadJson = JsonConvert.SerializeObject(snapshot, JsonSettings);
                var newChunk = BuildITxtChunk(Keyword, payloadJson);

                using (var src = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.SequentialScan))
                using (var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    var sig = new byte[PngSignature.Length];
                    if (src.Read(sig, 0, sig.Length) != sig.Length || !ByteArraysEqual(sig, PngSignature))
                        return OperationResult.Failure($"Not a PNG: {filePath}");
                    dst.Write(sig, 0, sig.Length);

                    var lengthBuffer = new byte[4];
                    var typeBuffer = new byte[4];
                    var crcBuffer = new byte[4];
                    var copyBuffer = new byte[CopyBufferSize];

                    var injected = false;

                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                            return OperationResult.Failure("Cancelled");

                        if (src.Read(lengthBuffer, 0, 4) != 4) break;
                        var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);

                        if (src.Read(typeBuffer, 0, 4) != 4)
                            return OperationResult.Failure($"Truncated PNG (chunk type) in {filePath}");

                        var isIend = ByteArraysEqual(typeBuffer, IendType);
                        var isITxt = ByteArraysEqual(typeBuffer, ITxtType);
                        var isInspectableITxt = isITxt && length > 0 && length < int.MaxValue;

                        if (isIend && !injected)
                        {
                            // Insert our chunk just before IEND.
                            dst.Write(newChunk, 0, newChunk.Length);
                            injected = true;
                        }

                        if (isInspectableITxt)
                        {
                            var dataBuffer = ReadFully(src, (int)length);
                            if (dataBuffer == null)
                                return OperationResult.Failure($"Truncated iTXt data in {filePath}");

                            if (HasOurKeyword(dataBuffer))
                            {
                                // Drop the existing Sightseeingway chunk (data + trailing CRC).
                                if (src.Read(crcBuffer, 0, 4) != 4)
                                    return OperationResult.Failure($"Truncated iTXt CRC in {filePath}");
                                continue;
                            }

                            // Pass-through: rewrite the chunk we already consumed.
                            dst.Write(lengthBuffer, 0, 4);
                            dst.Write(typeBuffer, 0, 4);
                            dst.Write(dataBuffer, 0, dataBuffer.Length);
                            if (src.Read(crcBuffer, 0, 4) != 4)
                                return OperationResult.Failure($"Truncated iTXt CRC in {filePath}");
                            dst.Write(crcBuffer, 0, 4);
                        }
                        else
                        {
                            // Pass through any non-iTXt chunk verbatim, streaming the data.
                            dst.Write(lengthBuffer, 0, 4);
                            dst.Write(typeBuffer, 0, 4);

                            var remaining = (int)length;
                            while (remaining > 0)
                            {
                                var n = src.Read(copyBuffer, 0, Math.Min(remaining, copyBuffer.Length));
                                if (n <= 0) return OperationResult.Failure($"Truncated chunk data in {filePath}");
                                dst.Write(copyBuffer, 0, n);
                                remaining -= n;
                            }

                            if (src.Read(crcBuffer, 0, 4) != 4)
                                return OperationResult.Failure($"Truncated chunk CRC in {filePath}");
                            dst.Write(crcBuffer, 0, 4);
                        }

                        if (isIend) break;
                    }

                    if (!injected)
                        return OperationResult.Failure($"PNG missing IEND or stream truncated: {filePath}");
                }

                File.Delete(filePath);
                File.Move(tmpPath, filePath);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* best effort */ }
                return OperationResult.Failure($"PNG injection failed for {filePath}", ex);
            }
        }

        private static byte[]? ReadFully(Stream src, int count)
        {
            var buffer = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = src.Read(buffer, read, count - read);
                if (n <= 0) return null;
                read += n;
            }
            return buffer;
        }

        private static bool HasOurKeyword(byte[] iTxtData)
        {
            var nullIdx = Array.IndexOf(iTxtData, (byte)0);
            if (nullIdx <= 0) return false;
            var keyword = Encoding.Latin1.GetString(iTxtData, 0, nullIdx);
            return keyword == Keyword;
        }

        /// <summary>
        /// Builds a complete iTXt chunk (length + type + data + CRC) with
        /// uncompressed UTF-8 text and empty language/translated-keyword fields.
        /// </summary>
        private static byte[] BuildITxtChunk(string keyword, string text)
        {
            var keywordBytes = Encoding.Latin1.GetBytes(keyword);
            var textBytes = Encoding.UTF8.GetBytes(text);

            // Layout: keyword \0 [compFlag=0] [compMethod=0] languageTag \0 translatedKeyword \0 text
            var dataLen = keywordBytes.Length + 1 + 1 + 1 + 0 + 1 + 0 + 1 + textBytes.Length;
            var data = new byte[dataLen];

            var pos = 0;
            Buffer.BlockCopy(keywordBytes, 0, data, pos, keywordBytes.Length); pos += keywordBytes.Length;
            data[pos++] = 0;        // null after keyword
            data[pos++] = 0;        // compression flag (0 = uncompressed)
            data[pos++] = 0;        // compression method (0 = zlib, ignored when flag is 0)
            data[pos++] = 0;        // null after language tag (empty)
            data[pos++] = 0;        // null after translated keyword (empty)
            Buffer.BlockCopy(textBytes, 0, data, pos, textBytes.Length);

            var chunk = new byte[4 + 4 + data.Length + 4];

            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)data.Length);
            Buffer.BlockCopy(ITxtType, 0, chunk, 4, 4);
            Buffer.BlockCopy(data, 0, chunk, 8, data.Length);

            var crc = Crc32.Compute(ITxtType, data);
            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length, 4), crc);

            return chunk;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
