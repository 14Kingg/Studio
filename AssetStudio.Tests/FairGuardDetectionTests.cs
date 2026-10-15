using AssetStudio;
using Xunit;

namespace AssetStudio.Tests;

public sealed class FairGuardDetectionTests
{
    private static readonly byte[] ZeroHash = new byte[16];
    private static readonly string[] CabGuidNode = { "CAB-0123456789abcdef0123456789abcdef" };

    [Fact]
    public void DetectsBundleWhenMultipleFairGuardIndicatorsMatch()
    {
        var result = FairGuardUtils.IsLikelyFairGuard(
            archiveFlags: 0x243,
            uncompressedDataHash: ZeroHash,
            nodePaths: CabGuidNode,
            firstBlockFlags: 3,
            firstBlockUncompressedSize: 128 * 1024);

        Assert.True(result);
    }

    [Fact]
    public void AcceptsStreamedBitAlongsideLz4HcCompression()
    {
        var result = FairGuardUtils.IsLikelyFairGuard(
            archiveFlags: 0x243,
            uncompressedDataHash: ZeroHash,
            nodePaths: CabGuidNode,
            firstBlockFlags: 0x43,
            firstBlockUncompressedSize: 128 * 1024);

        Assert.True(result);
    }

    [Fact]
    public void DoesNotDetectWhenHashIsNotZero()
    {
        var hash = new byte[16];
        hash[0] = 1;

        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, hash, CabGuidNode, 3, 128 * 1024));
    }

    [Fact]
    public void DoesNotDetectWithoutPaddingFlag()
    {
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x43, ZeroHash, CabGuidNode, 3, 128 * 1024));
    }

    [Fact]
    public void DoesNotDetectWithoutCabGuidNode()
    {
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, ZeroHash, new[] { "assets/main.assets" }, 3, 128 * 1024));
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, ZeroHash, new[] { "CAB-not-a-guid" }, 3, 128 * 1024));
    }

    [Fact]
    public void DoesNotDetectUnsupportedFirstBlockCompression()
    {
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, ZeroHash, CabGuidNode, 0, 128 * 1024));
    }

    [Fact]
    public void DoesNotDetectMissingHashOrEmptyBlocks()
    {
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, null!, CabGuidNode, 3, 128 * 1024));
        Assert.False(FairGuardUtils.IsLikelyFairGuard(0x243, ZeroHash, CabGuidNode, 3, 0));
    }
}
