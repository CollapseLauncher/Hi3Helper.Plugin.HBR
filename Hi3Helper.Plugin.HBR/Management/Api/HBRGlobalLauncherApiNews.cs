﻿using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

#if USELIGHTWEIGHTJSONPARSER
using System.IO;
#else
using System.Text.Json;
#endif

namespace Hi3Helper.Plugin.HBR.Management.Api;

[GeneratedComClass]
internal partial class HBRGlobalLauncherApiNews(string apiResponseBaseUrl, string gameTag, string authSalt1, string authSalt2) : LauncherApiNewsBase
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

    private HBRApiResponse<HBRApiResponseSocial>? SocialApiResponse { get; set; }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        using HttpResponseMessage message = await ApiResponseHttpClient.GetAsync(ApiResponseBaseUrl + "api/launcher/social/media/resource", HttpCompletionOption.ResponseHeadersRead, token);
        message.EnsureSuccessStatusCode();

#if USELIGHTWEIGHTJSONPARSER
        await using Stream networkStream = await message.Content.ReadAsStreamAsync(token);
        SocialApiResponse = await HBRApiResponse<HBRApiResponseSocial>.ParseFromAsync(networkStream, token: token);
#else
        string jsonResponse = await message.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger.LogTrace("API Social Media and News response: {JsonResponse}", jsonResponse);
        SocialApiResponse = JsonSerializer.Deserialize<HBRApiResponse<HBRApiResponseSocial>>(jsonResponse, HBRApiResponseContext.Default.HBRApiResponseHBRApiResponseSocial);
#endif

        SocialApiResponse!.EnsureSuccessCode();

        // Initialize embedded Icon data
        await HBRIconData.Initialize(token);

        return !message.IsSuccessStatusCode ? (int)message.StatusCode : 0;
    }

    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
        => InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);

    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
        => InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        try
        {
            if (SocialApiResponse?.ResponseData?.SocialMediaEntries == null ||
                SocialApiResponse.ResponseData.SocialMediaEntries.Count == 0)
            {
                SharedStatic.InstanceLogger.LogTrace("[HBRGlobalLauncherApiNews::GetSocialMediaEntries] API provides no Social Media entries!");
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            List<HBRApiResponseSocialResponse> validEntries = [..SocialApiResponse.ResponseData.SocialMediaEntries
                .Where(x => !string.IsNullOrEmpty(x.SocialMediaName) &&
                            !string.IsNullOrEmpty(x.ClickUrl) &&
                            HBRIconData.EmbeddedDataDictionary.ContainsKey(x.SocialMediaName)
                )];

            int entryCount = validEntries.Count;
            PluginDisposableMemory<LauncherSocialMediaEntry> memory = PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(entryCount);

            handle = memory.AsSafePointer();
            count = entryCount;
            isDisposable = true;

            SharedStatic.InstanceLogger.LogTrace("[HBRGlobalLauncherApiNews::GetSocialMediaEntries] {EntryCount} entries are allocated at: 0x{Address:x8}", entryCount, handle);

            for (int i = 0; i < entryCount; i++)
            {
                string socialMediaName = validEntries[i].SocialMediaName!;
                string clickUrl = validEntries[i].ClickUrl!;
                string? qrImageUrl = validEntries[i].QrImageUrl;

                byte[]? iconData = HBRIconData.GetEmbeddedData(socialMediaName);
                if (iconData == null)
                {
                    continue;
                }

                ref LauncherSocialMediaEntry unmanagedEntry = ref memory[i];
                if (!string.IsNullOrEmpty(qrImageUrl))
                {
                    unmanagedEntry.WriteQrImage(qrImageUrl);
                }

                unmanagedEntry.WriteIcon(iconData);
                unmanagedEntry.WriteDescription(socialMediaName);
                unmanagedEntry.WriteClickUrl(clickUrl);
            }

            isAllocated = true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError(ex, "Failed to get social media entries");
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
        }
    }

    private static void InitializeEmpty(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
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

            SocialApiResponse = null;
            base.Dispose();
        }
    }
}
