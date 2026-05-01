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
    /// JPEG writer that embeds the v1 metadata payload as an XMP packet inside
    /// an APP1 segment. Streams the file rather than buffering it.
    ///
    /// Idempotent: any existing APP1 segment whose XMP namespace identifies
    /// itself as a Sightseeingway packet is dropped from the output, so
    /// re-injection replaces rather than appends.
    /// Atomic: writes to <c>&lt;target&gt;.tmp</c> and renames over the original.
    ///
    /// Other plugins' XMP (Adobe-namespaced APP1) is preserved as-is.
    /// </summary>
    public sealed class JpegMetadataWriter : IMetadataWriter
    {
        private const string XmpNamespaceMarker = "http://ns.adobe.com/xap/1.0/\0";
        private const string SightseeingwayNamespace = "https://gposingway.github.io/Sightseeingway/schema/v1";
        private const int CopyBufferSize = 64 * 1024;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string Name => "jpeg";

        public OperationResult Write(string filePath, StateSnapshot snapshot, CancellationToken ct)
        {
            var tmpPath = filePath + ".tmp";

            try
            {
                var newApp1 = BuildXmpApp1Segment(snapshot);

                using (var src = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.SequentialScan))
                using (var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    // SOI: FF D8.
                    var soi = new byte[2];
                    if (src.Read(soi, 0, 2) != 2 || soi[0] != 0xFF || soi[1] != 0xD8)
                        return OperationResult.Failure($"Not a JPEG: {filePath}");
                    dst.Write(soi, 0, 2);

                    // Insert our APP1 immediately after SOI; pass through the rest, dropping any prior Sightseeingway APP1.
                    dst.Write(newApp1, 0, newApp1.Length);

                    var copyBuffer = new byte[CopyBufferSize];

                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                            return OperationResult.Failure("Cancelled");

                        var b1 = src.ReadByte();
                        if (b1 < 0) break;
                        if (b1 != 0xFF) return OperationResult.Failure($"Malformed JPEG segment marker in {filePath}");

                        var marker = src.ReadByte();
                        if (marker < 0) return OperationResult.Failure($"Truncated JPEG marker in {filePath}");

                        // SOS (Start of Scan, FF DA) — copy through and stream the rest verbatim.
                        if (marker == 0xDA)
                        {
                            dst.WriteByte(0xFF);
                            dst.WriteByte((byte)marker);
                            int n;
                            while ((n = src.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
                                dst.Write(copyBuffer, 0, n);
                            break;
                        }

                        // Standalone markers without length payload.
                        if (marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0x01 || marker == 0x00)
                        {
                            dst.WriteByte(0xFF);
                            dst.WriteByte((byte)marker);
                            if (marker == 0xD9) break; // EOI
                            continue;
                        }

                        // Length-prefixed segment.
                        var lenBytes = new byte[2];
                        if (src.Read(lenBytes, 0, 2) != 2)
                            return OperationResult.Failure($"Truncated JPEG segment length in {filePath}");
                        var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
                        if (segmentLength < 2)
                            return OperationResult.Failure($"Invalid JPEG segment length in {filePath}");

                        var payload = new byte[segmentLength - 2];
                        var read = 0;
                        while (read < payload.Length)
                        {
                            var n = src.Read(payload, read, payload.Length - read);
                            if (n <= 0) return OperationResult.Failure($"Truncated JPEG segment payload in {filePath}");
                            read += n;
                        }

                        // APP1 with our XMP namespace? Drop it.
                        if (marker == 0xE1 && IsOurXmpSegment(payload))
                            continue;

                        dst.WriteByte(0xFF);
                        dst.WriteByte((byte)marker);
                        dst.Write(lenBytes, 0, 2);
                        dst.Write(payload, 0, payload.Length);
                    }
                }

                File.Delete(filePath);
                File.Move(tmpPath, filePath);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* best effort */ }
                return OperationResult.Failure($"JPEG injection failed for {filePath}", ex);
            }
        }

        private static bool IsOurXmpSegment(byte[] payload)
        {
            var marker = Encoding.ASCII.GetBytes(XmpNamespaceMarker);
            if (payload.Length < marker.Length) return false;
            for (var i = 0; i < marker.Length; i++)
                if (payload[i] != marker[i]) return false;

            var xmpStart = marker.Length;
            if (xmpStart >= payload.Length) return false;

            // Cheap detection: presence of our schema namespace anywhere in the XMP body
            // is sufficient to identify our packet.
            var body = Encoding.UTF8.GetString(payload, xmpStart, payload.Length - xmpStart);
            return body.Contains(SightseeingwayNamespace, StringComparison.Ordinal);
        }

        private static byte[] BuildXmpApp1Segment(StateSnapshot snapshot)
        {
            var payloadJson = JsonConvert.SerializeObject(snapshot, JsonSettings);
            var xmp = BuildXmpPacket(payloadJson);

            var nsBytes = Encoding.ASCII.GetBytes(XmpNamespaceMarker);
            var xmpBytes = Encoding.UTF8.GetBytes(xmp);

            // APP1 = FF E1, length is u16 BE including length bytes themselves.
            var segmentBodyLen = nsBytes.Length + xmpBytes.Length;
            var totalLen = 2 + segmentBodyLen;
            if (totalLen > ushort.MaxValue)
                throw new InvalidOperationException(
                    $"XMP packet ({xmpBytes.Length} bytes) exceeds JPEG APP1 size limit.");

            var segment = new byte[2 + 2 + segmentBodyLen];
            segment[0] = 0xFF;
            segment[1] = 0xE1;
            BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2, 2), (ushort)totalLen);
            Buffer.BlockCopy(nsBytes, 0, segment, 4, nsBytes.Length);
            Buffer.BlockCopy(xmpBytes, 0, segment, 4 + nsBytes.Length, xmpBytes.Length);

            return segment;
        }

        private static string BuildXmpPacket(string payloadJson)
        {
            // CDATA-escape any closing brackets that could prematurely terminate the section.
            var safeJson = payloadJson.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal);

            return
                "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" +
                "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" x:xmptk=\"Sightseeingway\">" +
                  "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
                    $"<rdf:Description rdf:about=\"\" xmlns:sw=\"{SightseeingwayNamespace}\">" +
                      $"<sw:json><![CDATA[{safeJson}]]></sw:json>" +
                    "</rdf:Description>" +
                  "</rdf:RDF>" +
                "</x:xmpmeta>" +
                "<?xpacket end=\"w\"?>";
        }
    }
}
