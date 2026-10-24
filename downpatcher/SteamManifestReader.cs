using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EternalDownpatcher.WinForms;

internal sealed record ManifestFile(string Path, long Size, string Sha1);
internal sealed record ManifestRequest(int DepotId, string ManifestId);
internal sealed record SteamManifest(int DepotId, string ManifestId, ManifestFile[] Files);

// SteamKit's protobuf manifest envelope, with only file identity fields decoded
// Chunk payloads are skipped, DepotDownloader remains responsible for downloading
internal static class SteamManifestReader
{
    internal const int MaxBytes = 64 * 1024 * 1024;
    private const uint PayloadMagic = 0x71F617D0, MetadataMagic = 0x1F4812BE,
        SignatureMagic = 0x1B81B817, EndMagic = 0x32C415AB;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(index =>
    {
        uint value = (uint)index;
        for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xedb88320u);
        return value;
    }).ToArray();

    internal static SteamManifest Load(string path, ManifestRequest expected, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); PathSafety.RejectLinks(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length < 28 || input.Length > MaxBytes) throw new InvalidDataException("Invalid manifest size.");
        byte[] bytes = new byte[(int)input.Length]; input.ReadExactly(bytes);
        string checksum = path + ".sha";
        if (File.Exists(checksum))
        {
            PathSafety.RejectLinks(checksum);
            if (new FileInfo(checksum).Length != 20 || !SHA1.HashData(bytes).AsSpan().SequenceEqual(File.ReadAllBytes(checksum)))
                throw new InvalidDataException("Manifest checksum mismatch.");
        }
        return Parse(bytes, expected, token);
    }

    internal static SteamManifest Parse(ReadOnlySpan<byte> data, ManifestRequest expected, CancellationToken token = default)
    {
        if (data.Length < 28 || data.Length > MaxBytes) throw new InvalidDataException("Invalid manifest size.");
        ReadOnlySpan<byte> payload = default, metadata = default;
        bool signature = false, ended = false, hasPayload = false, hasMetadata = false;
        while (!data.IsEmpty)
        {
            if (data.Length < 4) throw new InvalidDataException("Truncated manifest envelope.");
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data); data = data[4..];
            if (magic == EndMagic) { ended = data.IsEmpty; break; }
            if (data.Length < 4) throw new InvalidDataException("Truncated manifest section.");
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(data); data = data[4..];
            if (length > (uint)data.Length) throw new InvalidDataException("Invalid manifest section length.");
            var section = data[..(int)length]; data = data[(int)length..];
            if (magic == PayloadMagic && !hasPayload) { payload = section; hasPayload = true; }
            else if (magic == MetadataMagic && !hasMetadata) { metadata = section; hasMetadata = true; }
            else if (magic == SignatureMagic && !signature) signature = true;
            else throw new InvalidDataException("Unsupported or repeated manifest section.");
        }
        if (!ended || !hasPayload || !hasMetadata || metadata.IsEmpty || !signature) throw new InvalidDataException("Incomplete manifest envelope.");
        int depot = 0; ulong manifest = 0; bool encrypted = false, hasCrc = false; uint expectedCrc = 0;
        var reader = new ProtoReader(metadata);
        while (reader.Read(out int field, out int wire, out ulong number, out _))
        {
            if (field is 1 or 2 or 4 or 9 && wire != 0) throw new InvalidDataException("Invalid manifest metadata.");
            if (field == 1) { if (number > int.MaxValue) throw new InvalidDataException("Invalid depot ID."); depot = (int)number; }
            if (field == 2) manifest = number;
            if (field == 4) encrypted = number != 0;
            if (field == 9) { if (number > uint.MaxValue) throw new InvalidDataException("Invalid manifest CRC."); expectedCrc = (uint)number; hasCrc = true; }
        }
        if (depot != expected.DepotId || manifest.ToString(System.Globalization.CultureInfo.InvariantCulture) != expected.ManifestId || encrypted)
            throw new InvalidDataException("Wrong depot/manifest ID or encrypted file names.");
        Span<byte> lengthBytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, payload.Length);
        if (!hasCrc || Crc32(lengthBytes, payload) != expectedCrc) throw new InvalidDataException("Manifest payload CRC mismatch.");
        List<ManifestFile> files = []; HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        reader = new ProtoReader(payload);
        while (reader.Read(out int field, out int wire, out _, out var mapping))
        {
            token.ThrowIfCancellationRequested();
            if (field != 1) continue;
            if (wire != 2 || files.Count >= 100000) throw new InvalidDataException("Invalid manifest file list.");
            ManifestFile? file = ReadFile(mapping);
            if (file is null) continue;
            if (!names.Add(file.Path)) throw new InvalidDataException("Duplicate file in a depot manifest.");
            files.Add(file);
        }
        return new(depot, expected.ManifestId, files.ToArray());
    }

    private static ManifestFile? ReadFile(ReadOnlySpan<byte> bytes)
    {
        string? name = null, hash = null; ulong size = 0, flags = 0; string? link = null;
        var fields = new ProtoReader(bytes);
        while (fields.Read(out int field, out int wire, out ulong number, out var value))
        {
            if (field is 1 or 5 or 7 && wire != 2 || field is 2 or 3 && wire != 0)
                throw new InvalidDataException("Invalid manifest file metadata.");
            if (field == 1) name = Utf8.GetString(value);
            if (field == 2) size = number;
            if (field == 3) flags = number;
            if (field == 5) { if (value.Length is not 0 and not 20) throw new InvalidDataException("Invalid file SHA-1."); hash = value.IsEmpty ? null : Convert.ToHexString(value); }
            if (field == 7) link = Utf8.GetString(value);
        }
        if (!string.IsNullOrEmpty(link)) throw new InvalidDataException("Linked game files are unsupported.");
        if ((flags & 64) != 0) return null;
        if (name is null || hash is null || size > long.MaxValue) throw new InvalidDataException("Incomplete manifest file identity.");
        return new(NormalizePath(name), (long)size, hash);
    }

    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || Path.IsPathRooted(path)) throw new InvalidDataException("Invalid game-relative file name.");
        string normalized = path.Replace('\\', '/'); string[] parts = normalized.Split('/');
        if (parts[0].Equals(".DepotDownloader", StringComparison.OrdinalIgnoreCase) || parts.Any(part =>
            part is "" or "." or ".." || part.Length > 255 || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("Invalid game-relative file name.");
        return normalized;
    }

    internal static uint Crc32(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> payload)
    {
        uint value = uint.MaxValue;
        foreach (byte b in prefix) value = CrcTable[(value ^ b) & 255] ^ (value >> 8);
        foreach (byte b in payload) value = CrcTable[(value ^ b) & 255] ^ (value >> 8);
        return ~value;
    }

    private ref struct ProtoReader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> _remaining = data;
        internal bool Read(out int field, out int wire, out ulong number, out ReadOnlySpan<byte> bytes)
        {
            field = wire = 0; number = 0; bytes = default;
            if (_remaining.IsEmpty) return false;
            ulong key = Varint();
            if (key >> 3 is 0 or > 536870911) throw new InvalidDataException("Invalid protobuf tag.");
            field = (int)(key >> 3); wire = (int)(key & 7);
            if (wire == 0) number = Varint();
            else if (wire == 2) { ulong length = Varint(); if (length > (ulong)_remaining.Length) throw new InvalidDataException("Truncated protobuf field."); bytes = Take((int)length); }
            else if (wire is 1 or 5) bytes = Take(wire == 1 ? 8 : 4);
            else throw new InvalidDataException("Unsupported protobuf wire type.");
            return true;
        }
        private ReadOnlySpan<byte> Take(int count)
        {
            if (_remaining.Length < count) throw new InvalidDataException("Truncated protobuf field.");
            var result = _remaining[..count]; _remaining = _remaining[count..]; return result;
        }
        private ulong Varint()
        {
            ulong result = 0;
            for (int shift = 0; shift <= 63; shift += 7)
            {
                byte b = Take(1)[0];
                if (shift == 63 && b > 1) throw new InvalidDataException("Protobuf integer overflow.");
                result |= (ulong)(b & 127) << shift;
                if ((b & 128) == 0) return result;
            }
            throw new InvalidDataException("Invalid protobuf integer.");
        }
    }
}
