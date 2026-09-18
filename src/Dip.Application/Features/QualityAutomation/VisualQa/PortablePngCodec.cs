// Purpose: Implements application behavior for P or ta bl eP ng Co de c.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed record PortablePngImage(int Width, int Height, byte[] Rgba);

public static class PortablePngCodec
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static async Task<PortablePngImage> DecodeAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var input = new MemoryStream();
        await stream.CopyToAsync(input, ct);
        return Decode(input.ToArray());
    }

    public static PortablePngImage Decode(byte[] bytes)
    {
        if (bytes.Length < Signature.Length || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Visual QA currently supports PNG input only.");

        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        using var idat = new MemoryStream();
        var offset = 8;
        while (offset + 12 <= bytes.Length)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            offset += 8;
            if (length < 0 || offset + length + 4 > bytes.Length) throw new InvalidDataException("PNG chunk is truncated.");
            var payload = bytes.AsSpan(offset, length);
            offset += length + 4; // payload + CRC

            if (type == "IHDR")
            {
                if (length != 13) throw new InvalidDataException("PNG IHDR is invalid.");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[..4]));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)));
                bitDepth = payload[8];
                colorType = payload[9];
                if (payload[10] != 0 || payload[11] != 0) throw new InvalidDataException("Unsupported PNG compression/filter method.");
                interlace = payload[12];
            }
            else if (type == "IDAT") idat.Write(payload);
            else if (type == "IEND") break;
        }

        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG has no valid dimensions.");
        if (width > 16384 || height > 16384) throw new InvalidDataException("PNG dimensions exceed the Phase-4 safety limit.");
        if (bitDepth != 8 || (colorType != 2 && colorType != 6) || interlace != 0)
            throw new InvalidDataException("Visual QA supports non-interlaced 8-bit RGB/RGBA PNG images.");

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var stride = checked(width * bytesPerPixel);
        var expected = checked((stride + 1) * height);
        byte[] raw;
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        using (var output = new MemoryStream(expected))
        {
            z.CopyTo(output);
            raw = output.ToArray();
        }
        if (raw.Length != expected) throw new InvalidDataException("PNG decompressed size is unexpected.");

        var recon = new byte[checked(stride * height)];
        var previous = new byte[stride];
        var current = new byte[stride];
        var src = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = raw[src++];
            Array.Copy(raw, src, current, 0, stride);
            src += stride;
            Unfilter(current, previous, bytesPerPixel, filter);
            Array.Copy(current, 0, recon, y * stride, stride);
            (previous, current) = (current, previous);
        }

        var rgba = new byte[checked(width * height * 4)];
        for (var i = 0; i < width * height; i++)
        {
            var si = i * bytesPerPixel;
            var di = i * 4;
            rgba[di] = recon[si];
            rgba[di + 1] = recon[si + 1];
            rgba[di + 2] = recon[si + 2];
            rgba[di + 3] = bytesPerPixel == 4 ? recon[si + 3] : (byte)255;
        }
        return new PortablePngImage(width, height, rgba);
    }

    public static byte[] EncodeRgba(int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Invalid RGBA image.");
        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[..4], (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4, 4), (uint)height);
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        WriteChunk(output, "IHDR", ihdr.ToArray());

        using var raw = new MemoryStream();
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * stride, stride);
        }
        raw.Position = 0;
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) raw.CopyTo(z);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Unfilter(byte[] row, byte[] previous, int bpp, byte filter)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : 0;
            var up = previous[i];
            var upLeft = i >= bpp ? previous[i - bpp] : 0;
            row[i] = filter switch
            {
                0 => row[i],
                1 => unchecked((byte)(row[i] + left)),
                2 => unchecked((byte)(row[i] + up)),
                3 => unchecked((byte)(row[i] + ((left + up) >> 1))),
                4 => unchecked((byte)(row[i] + Paeth(left, up, upLeft))),
                _ => throw new InvalidDataException($"Unsupported PNG filter {filter}.")
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcInput));
        output.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
        }
        return crc ^ 0xffffffff;
    }
}
