using Wjss.Adapters.MotionLibrary;
using Xunit;

namespace Wjss.MotionLibrary.Tests;

/// <summary>
/// The file-shape proof layer: expected-hash parsing, constant-time equality, PE machine
/// extraction and path containment, all on SYNTHETIC in-memory material. No real native module
/// image or hash appears here.
/// </summary>
public sealed class NativeFileInspectionTests
{
    private const string Hash64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal static byte[] MakePeImage(ushort machine, int size = 4096, int lfanew = 0x80)
    {
        var bytes = new byte[size];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BitConverter.GetBytes(lfanew).CopyTo(bytes, 0x3C);
        bytes[lfanew] = (byte)'P';
        bytes[lfanew + 1] = (byte)'E';
        BitConverter.GetBytes(machine).CopyTo(bytes, lfanew + 4);
        // Deterministic non-zero fill outside the header so variants hash differently.
        for (var index = lfanew + 24; index < bytes.Length; index++)
        {
            bytes[index] = 0x5A;
        }

        return bytes;
    }

    [Fact]
    public void A_64_Hex_Expected_Hash_Parses_Into_32_Bytes()
    {
        Assert.True(NativeFileInspection.TryParseExpectedHash(Hash64, out var hash));
        Assert.Equal(32, hash.Length);
        Assert.Equal((byte)0x01, hash[0]);
        Assert.Equal((byte)0x23, hash[1]);
        Assert.Equal((byte)0xEF, hash[31]);

        Assert.True(NativeFileInspection.TryParseExpectedHash(Hash64.ToUpperInvariant(), out var upper));
        Assert.Equal(hash, upper);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]   // 63
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef00")] // 65
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]   // non-hex
    [InlineData("01234567 9abcdef0123456789abcdef0123456789abcdef0123456789abcde")]   // space
    public void A_Malformed_Expected_Hash_Never_Parses(string raw)
    {
        Assert.False(NativeFileInspection.TryParseExpectedHash(raw, out var hash));
        Assert.Empty(hash);
    }

    [Fact]
    public void Hash_Equality_Is_Exact_And_Fixed_Time()
    {
        var a = Convert.FromHexString(Hash64.ToUpperInvariant());
        var same = Convert.FromHexString(Hash64.ToUpperInvariant());
        var different = Convert.FromHexString(Hash64.ToUpperInvariant());
        different[31] ^= 0xFF;

        Assert.True(NativeFileInspection.HashesEqual(a, same));
        Assert.False(NativeFileInspection.HashesEqual(a, different));
        Assert.False(NativeFileInspection.HashesEqual(a, Array.Empty<byte>()));
        Assert.False(NativeFileInspection.HashesEqual(a, new byte[31]));
    }

    [Fact]
    public void A_Synthetic_X64_Image_Parses_As_x64()
    {
        Assert.True(NativeFileInspection.TryReadPeMachine(MakePeImage(0x8664), out var machine));
        Assert.Equal(NativeFileInspection.PeMachineX64, machine);
    }

    [Theory]
    [InlineData((ushort)0x014C)] // x86
    [InlineData((ushort)0xAA64)] // arm64
    [InlineData((ushort)0x0200)] // ia64
    public void A_Non_x64_Image_Does_Not_Equal_The_Approved_Architecture(ushort machine)
    {
        Assert.True(NativeFileInspection.TryReadPeMachine(MakePeImage(machine), out var parsed));
        Assert.NotEqual(NativeFileInspection.PeMachineX64, parsed);
    }

    [Fact]
    public void A_Structurally_Invalid_Image_Yields_No_Machine()
    {
        Assert.False(NativeFileInspection.TryReadPeMachine(Array.Empty<byte>(), out _));
        Assert.False(NativeFileInspection.TryReadPeMachine(new byte[32], out _));

        var noMz = MakePeImage(0x8664);
        noMz[0] = 0x00;
        Assert.False(NativeFileInspection.TryReadPeMachine(noMz, out _));

        var badLfanew = MakePeImage(0x8664);
        BitConverter.GetBytes(badLfanew.Length + 64).CopyTo(badLfanew, 0x3C);
        Assert.False(NativeFileInspection.TryReadPeMachine(badLfanew, out _));

        var negativeLfanew = MakePeImage(0x8664);
        BitConverter.GetBytes(-4).CopyTo(negativeLfanew, 0x3C);
        Assert.False(NativeFileInspection.TryReadPeMachine(negativeLfanew, out _));

        var noPeSignature = MakePeImage(0x8664);
        noPeSignature[0x80] = (byte)'X';
        Assert.False(NativeFileInspection.TryReadPeMachine(noPeSignature, out _));

        var truncated = MakePeImage(0x8664)[..0x90];
        Assert.False(NativeFileInspection.TryReadPeMachine(truncated, out _));
    }

    [Fact]
    public void Containment_Admits_A_Direct_Child_And_Refuses_Escapes()
    {
        Assert.True(NativeFileInspection.TryCombineContained(
            "C:\\Synthetic\\Approved", "gclib.dll", out var fullPath));
        Assert.Equal("C:\\Synthetic\\Approved\\gclib.dll", fullPath);

        Assert.False(NativeFileInspection.TryCombineContained("C:\\Synthetic\\Approved", "..\\gclib.dll", out _));
        Assert.False(NativeFileInspection.TryCombineContained("C:\\Synthetic\\Approved", "sub\\gclib.dll", out _));
        Assert.False(NativeFileInspection.TryCombineContained("C:\\Synthetic\\Approved", "sub/gclib.dll", out _));
        Assert.False(NativeFileInspection.TryCombineContained("C:\\Synthetic\\Approved", "gclib.dll:stream", out _));
    }
}
