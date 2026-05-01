namespace Sightseeingway.Metadata.Writers
{
    /// <summary>
    /// PNG-flavoured CRC32 (polynomial 0xEDB88320, initial 0xFFFFFFFF, output XOR'd).
    /// Identical to IEEE 802.3 CRC. Lazy-initialised lookup table.
    /// </summary>
    internal static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(byte[] buffer, int offset, int count)
        {
            var crc = 0xFFFFFFFFu;
            for (var i = 0; i < count; i++)
            {
                var b = buffer[offset + i];
                crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
            }
            return crc ^ 0xFFFFFFFFu;
        }

        public static uint Compute(byte[] a, byte[] b)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var x in a) crc = (crc >> 8) ^ Table[(crc ^ x) & 0xFF];
            foreach (var x in b) crc = (crc >> 8) ^ Table[(crc ^ x) & 0xFF];
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }
    }
}
