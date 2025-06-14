﻿using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiMedia(string apiResponseBaseUrl, string gameTag, string authSalt1, string authSalt2) : LauncherApiMediaBase
{
    [field: AllowNull, MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(gameTag, true, true, authSalt1, authSalt2);
        set;
    }

    [field: AllowNull, MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= HBRUtility.CreateApiHttpClient(gameTag, false, false);
        set;
    }

    protected override string ApiResponseBaseUrl { get; } = apiResponseBaseUrl;

    private HBRApiResponse<HBRApiResponseMedia>? ApiResponse { get; set; }

    public override unsafe bool GetBackgroundEntries(out nint handle, out int count, out bool isDisposable)
    {
        using (ThisInstanceLock.EnterScope())
        {
            PluginDisposableMemory<LauncherPathEntry> backgroundEntries = PluginDisposableMemory<LauncherPathEntry>.Alloc();

            try
            {
                ref LauncherPathEntry entry = ref backgroundEntries[0];

                if (ApiResponse?.ResponseData == null)
                {
                    isDisposable = false;
                    handle = nint.Zero;
                    count = 0;
                    return false;
                }

                ulong fileHashCrc = ApiResponse.ResponseData.BackgroundImageChecksum;
                void* ptr = &fileHashCrc;

                entry.Write(ApiResponse.ResponseData.BackgroundImageUrl, new Span<byte>(ptr, sizeof(ulong)));
                return true;
            }
            finally
            {
                isDisposable = backgroundEntries.IsDisposable == 1;
                handle = backgroundEntries.AsSafePointer();
                count = backgroundEntries.Length;
            }
        }
    }

    public override bool GetLogoOverlayEntries(out nint handle, out int count, out bool isDisposable)
    {
        isDisposable = false;
        handle = nint.Zero;
        count = 0;
        return false;
    }

    public override LauncherBackgroundFlag GetBackgroundFlag()
        => LauncherBackgroundFlag.IsSourceFile | LauncherBackgroundFlag.TypeIsImage;

    public override LauncherBackgroundFlag GetLogoFlag()
        => LauncherBackgroundFlag.None;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        using HttpResponseMessage message = await ApiResponseHttpClient.GetAsync(ApiResponseBaseUrl + "api/launcher/base/config", HttpCompletionOption.ResponseHeadersRead, token);
        message.EnsureSuccessStatusCode();

        string jsonResponse = await message.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger?.LogTrace("API Media response: {JsonResponse}", jsonResponse);

        ApiResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseMedia>>(jsonResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseMedia);
        ApiResponse!.EnsureSuccessCode();

        return 0;
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
    }

    public override void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient.Dispose();
            ApiDownloadHttpClient = null!;

            ApiResponse = null;
            base.Dispose();
        }
    }
}
