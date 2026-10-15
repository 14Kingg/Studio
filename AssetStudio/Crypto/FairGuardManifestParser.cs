using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public sealed class FairGuardManifestParseResult
    {
        public IReadOnlyList<string> Strings { get; }
        public int StringTableEndOffset { get; }
        public int S2PathCount { get; }
        public int TrailingBinarySize { get; }
        public bool LooksLikeManifest => Strings.Count > 10 && S2PathCount > 0;

        internal FairGuardManifestParseResult(List<string> strings, int endOffset, int bodyLength)
        {
            Strings = strings.AsReadOnly();
            StringTableEndOffset = endOffset;
            S2PathCount = strings.Count(x => x.StartsWith("s2/", StringComparison.OrdinalIgnoreCase));
            TrailingBinarySize = Math.Max(0, bodyLength - endOffset);
        }
    }

    public sealed class FairGuardManifestExport
    {
        public string SourcePath { get; set; }
        public int ParsedStringCount { get; set; }
        public int S2PathCount { get; set; }
        public int TrailingBinarySize { get; set; }
        public bool BinaryIndexDecoded { get; set; }
        public IReadOnlyList<string> Strings { get; set; }
        public IReadOnlyList<string> S2Paths { get; set; }
    }

    /// <summary>
    /// Best-effort parser for the length-prefixed ASCII string table observed in
    /// FairGuard s2_manifest payloads. This does not claim to decode the trailing
    /// binary index/configuration section.
    /// </summary>
    public static class FairGuardManifestParser
    {
        public static FairGuardManifestParseResult ParseStringTable(byte[] body, int startOffset = 16)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (startOffset < 0 || startOffset > body.Length)
                throw new ArgumentOutOfRangeException(nameof(startOffset));

            var strings = new List<string>();
            var offset = startOffset;
            while (offset <= body.Length - 4)
            {
                var length = body[offset]
                    | body[offset + 1] << 8
                    | body[offset + 2] << 16
                    | body[offset + 3] << 24;

                if (length <= 0 || length > 512 || length > body.Length - offset - 4)
                    break;

                var valueOffset = offset + 4;
                var printableAscii = true;
                for (var i = 0; i < length; i++)
                {
                    var value = body[valueOffset + i];
                    if (value < 32 || value >= 127)
                    {
                        printableAscii = false;
                        break;
                    }
                }

                if (!printableAscii)
                    break;

                strings.Add(Encoding.ASCII.GetString(body, valueOffset, length));
                var next = valueOffset + length;
                offset = (next + 3) & ~3;
                if (offset > body.Length)
                    break;
            }

            return new FairGuardManifestParseResult(strings, offset, body.Length);
        }
    }
}
