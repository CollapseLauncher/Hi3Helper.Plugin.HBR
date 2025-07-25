﻿using Hi3Helper.Plugin.Core.Management;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers;
using System.Linq;


#if !USELIGHTWEIGHTJSONPARSER
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System.Text.Json.Serialization;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endif

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(HBRGameManifest))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentSize = 2,
    IndentCharacter = ' ',
    NewLine = "\n")]
public partial class HBRGameLauncherConfigContext : JsonSerializerContext;
#endif

public class HBRGameLauncherConfig
#if USELIGHTWEIGHTJSONPARSER
    : IJsonStreamSerializable<HBRGameLauncherConfig>,
      IJsonStreamParsable<HBRGameLauncherConfig>,
      IJsonElementParsable<HBRGameLauncherConfig>
#endif
{
#if USELIGHTWEIGHTJSONPARSER
    internal const string PropTag = "tag";
    internal const string PropName = "name";
    internal const string PropVersion = "version";
    internal const string PropSalt = "vc";

    private readonly JsonElement? _rootElement;

    private HBRGameLauncherConfig(JsonElement? rootElement)
    {
        _rootElement = rootElement;
    }

    public string? Tag
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            return _rootElement?.TryGetStringNonNullOrEmpty(PropTag, out string value) ?? false ? value : null;
        }
        set;
    }

    public string? Name
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            return _rootElement?.TryGetStringNonNullOrEmpty(PropName, out string value) ?? false ? value : null;
        }
        set;
    }

    public GameVersion Version
    {
        get
        {
            if (field != GameVersion.Empty || (_rootElement?.TryGetValue(PropVersion, out field) ?? false))
            {
                return field;
            }

            return GameVersion.Empty;
        }
        set;
    }

    public string Salt => GetConfigSalt(Tag, Name, Version.ToString("n"));

    public static HBRGameLauncherConfig CreateEmpty() => new(null);

    public static HBRGameLauncherConfig ParseFrom(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<HBRGameLauncherConfig> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default, CancellationToken token = default)
    {
        try
        {
            JsonDocument document = await JsonDocument.ParseAsync(stream, options, token);
            return ParseFrom(document.RootElement);
        }
        finally
        {
            if (isDisposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public static HBRGameLauncherConfig ParseFrom(JsonElement element) => new(element);

    public static async Task SerializeToStreamAsync(HBRGameLauncherConfig obj, Stream stream, bool isDisposeStream = false,
        JsonWriterOptions options = default, CancellationToken token = default)
    {
        await using Utf8JsonWriter writer = new Utf8JsonWriter(stream, options);

        try
        {
            SearchValues<string> searchValueKeys = SearchValues.Create([PropVersion, PropTag, PropName, PropSalt], StringComparison.OrdinalIgnoreCase);

            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(obj.Tag))
                writer.WriteString(PropTag, obj.Tag);

            if (!string.IsNullOrEmpty(obj.Name))
                writer.WriteString(PropName, obj.Name);

            if (obj.Version != GameVersion.Empty)
                writer.WriteString(PropVersion, obj.Version.ToString("n"));

            if (obj._rootElement.HasValue)
            {
                // Write the rest
                foreach (var element in obj._rootElement.Value
                    .EnumerateObject()
                    .Where(element => !element.Name.AsSpan().ContainsAny(searchValueKeys) && element.Value.ValueKind != JsonValueKind.Null))
                {
                    element.WriteTo(writer);
                }
            }

            // Append salt
            writer.WriteString(PropSalt, obj.Salt);

            writer.WriteEndObject();
            await writer.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (isDisposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }
#endif

    internal static string GetConfigSalt(params ReadOnlySpan<string?> salts)
    {
        string configSalt = string.Join(';', salts);

        Span<byte> configSaltInBytes = stackalloc byte[Encoding.UTF8.GetByteCount(configSalt)];
        _ = Encoding.UTF8.GetBytes(configSalt, configSaltInBytes);

        Span<byte> hashData = stackalloc byte[MD5.HashSizeInBytes];
        MD5.HashData(configSaltInBytes, hashData);

        return Convert.ToBase64String(hashData);
    }
}

public class HBRGameManifest
#if USELIGHTWEIGHTJSONPARSER
    : IJsonStreamSerializable<HBRGameManifest>,
      IJsonWriterSerializable<HBRGameManifest>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("basis")]
#endif
    public string? GamePackageBasis { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("name")]
#endif
    public string? GameTag { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("version")]
    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
#endif
    public GameVersion GameVersion { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("source")]
#endif
    public string? ParentDir { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("files")]
#endif
    public List<HBRGameManifestEntry>? ManifestEntries { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("vc")]
#endif
    public string ConfigSignature => HBRGameLauncherConfig.GetConfigSalt(GameTag, $"{GameVersion.ToString("n")}", GamePackageBasis);

#if USELIGHTWEIGHTJSONPARSER
    public static async Task SerializeToStreamAsync(HBRGameManifest obj, Stream stream, bool isDisposeStream = false, JsonWriterOptions options = default,
        CancellationToken token = default)
    {
        try
        {
            await using Utf8JsonWriter writer = new Utf8JsonWriter(stream, options);
            await SerializeToWriterAsync(obj, writer, token).ConfigureAwait(false);
        }
        finally
        {
            if (isDisposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public static async Task SerializeToWriterAsync(HBRGameManifest obj, Utf8JsonWriter writer, CancellationToken token = default)
    {
        writer.WriteStartObject();

        if (obj.GameTag != null)
            writer.WriteString("name"u8, obj.GameTag);

        if (obj.GameVersion != GameVersion.Empty)
            writer.WriteString("version"u8, obj.GameVersion.ToString("n"));

        if (obj.GamePackageBasis != null)
            writer.WriteString("basis"u8, obj.GamePackageBasis);

        writer.WriteString("vc"u8, obj.ConfigSignature);

        if (obj.ManifestEntries is { Count: > 0 })
        {
            writer.WriteStartArray("files"u8);
            foreach (HBRGameManifestEntry entry in obj.ManifestEntries)
            {
                await HBRGameManifestEntry.SerializeToWriterAsync(entry, writer, token).ConfigureAwait(false);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        await writer.FlushAsync(token).ConfigureAwait(false);
    }
#endif
}

public class HBRGameManifestEntry
#if USELIGHTWEIGHTJSONPARSER
    : IJsonWriterSerializable<HBRGameManifestEntry>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("hash")]
    [JsonConverter(typeof(HBRCrc64HashConverter))]
#endif
    public byte[]? AssetCrc64Hash { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonIgnore]
#endif
    public string AssetCrc64HashAsString => $"{BitConverter.ToUInt64(AssetCrc64Hash)}";
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("path")]
#endif
    public string? AssetPath { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
#endif
    public long AssetSize { get; set; }
    
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("vc")]
#endif
    public string ConfigSignature => HBRGameLauncherConfig.GetConfigSalt(AssetPath, AssetCrc64HashAsString, $"{AssetSize}");

#if USELIGHTWEIGHTJSONPARSER
    public static Task SerializeToWriterAsync(HBRGameManifestEntry obj, Utf8JsonWriter writer, CancellationToken token = default)
        => Task.Factory.StartNew(() =>
        {
            writer.WriteStartObject();

            writer.WriteString("path"u8, obj.AssetPath);
            writer.WriteString("hash"u8, obj.AssetCrc64HashAsString);
            writer.WriteString("size"u8, obj.AssetSize.ToString());
            writer.WriteString("vc"u8, obj.ConfigSignature);

            writer.WriteEndObject();
        }, token);
#endif
}

#if !USELIGHTWEIGHTJSONPARSER
public class HBRCrc64HashConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !ulong.TryParse(reader.ValueSpan, null, out ulong hash))
        {
            return null;
        }

        return BitConverter.GetBytes(hash);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        ulong byteAsUlong = BitConverter.ToUInt64(value);
        writer.WriteStringValue($"{byteAsUlong}");
    }
}
#endif