using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetStudio;
using Xunit;

namespace AssetStudio.Tests;

public sealed class FairGuardManifestParserTests
{
    private static byte[] BuildBody(params string[] values)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[16]);
        foreach (var value in values)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(BitConverter.GetBytes(bytes.Length));
            stream.Write(bytes);
            while ((stream.Position & 3) != 0)
                stream.WriteByte(0);
        }
        stream.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        return stream.ToArray();
    }

    [Fact]
    public void ParsesLengthPrefixedAsciiStringsAndAlignment()
    {
        var body = BuildBody("s2/config/a", "s2/config/b", "assets/main");
        var result = FairGuardManifestParser.ParseStringTable(body);

        Assert.Equal(new[] { "s2/config/a", "s2/config/b", "assets/main" }, result.Strings);
        Assert.True(result.StringTableEndOffset <= body.Length);
        Assert.Equal(2, result.S2PathCount);
        Assert.Equal(4, result.TrailingBinarySize);
    }

    [Fact]
    public void RecognizesManifestLikeStringTableOnlyWhenEnoughPathsExist()
    {
        var values = Enumerable.Range(0, 12).Select(i => $"s2/assets/{i}").ToArray();
        Assert.True(FairGuardManifestParser.ParseStringTable(BuildBody(values)).LooksLikeManifest);
        Assert.False(FairGuardManifestParser.ParseStringTable(BuildBody("s2/a", "s2/b")).LooksLikeManifest);
    }

    [Fact]
    public void StopsAtInvalidLengthWithoutThrowing()
    {
        var body = new byte[20];
        body[16] = 0xFF;
        body[17] = 0xFF;
        body[18] = 0xFF;
        body[19] = 0x7F;

        var result = FairGuardManifestParser.ParseStringTable(body);
        Assert.Empty(result.Strings);
    }

    [Fact]
    public void RejectsInvalidStartOffset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FairGuardManifestParser.ParseStringTable(new byte[8]));
    }
}
