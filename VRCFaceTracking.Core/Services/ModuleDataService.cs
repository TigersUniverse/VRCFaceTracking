﻿using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Contracts.Services;
using VRCFaceTracking.Core.Helpers;
using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Services;

public class ModuleDataService : IModuleDataService
{
    private List<InstallableTrackingModule>? _remoteModules;
    private readonly Dictionary<Guid, int> _ratingCache = new();

    private readonly IIdentityService _identityService;
    private readonly ILogger<ModuleDataService> _logger;
#if NET7_0_OR_GREATER
    private readonly HttpClient _httpClient;
#else
    private readonly WebClient _webClient = new WebClient();
    private Uri BaseUri = new Uri(BaseUrl);
#endif

    private const string BaseUrl = "https://rjlk4u22t36tvqz3bvbkwv675a0wbous.lambda-url.us-east-1.on.aws/";

    public ModuleDataService(IIdentityService identityService, ILogger<ModuleDataService> logger)
    {
        _identityService = identityService;
        _logger = logger;
#if NET7_0_OR_GREATER
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
#endif
    }

    private async Task<IEnumerable<InstallableTrackingModule>> AllModules()
    {
        // This is where we make the actual request to the API at modules and get the list of modules.
#if NET7_0_OR_GREATER
        var response = await _httpClient.GetAsync("modules");
        if (!response.IsSuccessStatusCode)
        {
            return new List<InstallableTrackingModule>();
        }

        var content = await response.Content.ReadAsStringAsync();
#else
        var content = _webClient.DownloadString(new Uri(BaseUri, "modules"));
#endif
        return await Json.ToObjectAsync<List<InstallableTrackingModule>>(content);
    }

    public async Task<IEnumerable<InstallableTrackingModule>> GetRemoteModules()
    {
        _remoteModules ??= new List<InstallableTrackingModule>(await AllModules());

        return _remoteModules;
    }

    public async Task IncrementDownloadsAsync(TrackingModuleMetadata moduleMetadata)
    {
        // Send a PATCH request to the downloads endpoint with the module ID in the body
        var rating = new RatingObject
            { UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString() };
#if NET7_0_OR_GREATER
        var content = new StringContent(JsonConvert.SerializeObject(rating), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync("downloads", content);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return;
        }
        _logger.LogError("Failed to increment downloads for {ModuleId}. Status code: {StatusCode}", moduleMetadata.ModuleId, response.StatusCode);
#else
        _webClient.UploadString(new Uri(BaseUri, "downloads"), "PATCH", JsonConvert.SerializeObject(rating));
#endif
    }

    public IEnumerable<InstallableTrackingModule> GetLegacyModules()
    {
        if (!Directory.Exists(Utils.CustomLibsDirectory))
        {
            Directory.CreateDirectory(Utils.CustomLibsDirectory);
        }

        var moduleDlls = Directory.GetFiles(Utils.CustomLibsDirectory, "*.dll");

        return moduleDlls.Select(moduleDll => new InstallableTrackingModule
        {
            AssemblyLoadPath = moduleDll,
            DllFileName = Path.GetFileName(moduleDll),
            InstallationState = InstallState.Installed,
            ModuleId = Guid.Empty,
            ModuleName = Path.GetFileNameWithoutExtension(moduleDll),
            ModuleDescription = "Legacy module",
            AuthorName = "Local",
            ModulePageUrl = "file:///" + Path.GetDirectoryName(moduleDll)
        });
    }

    public async Task<int?> GetMyRatingAsync(TrackingModuleMetadata moduleMetadata)
    {
        if (_ratingCache.TryGetValue(moduleMetadata.ModuleId, out var async))
        {
            _logger.LogDebug("Rating for {ModuleId} was cached as {Rating}", moduleMetadata.ModuleId, async);
            return async;
        }

        var rating = new RatingObject
            { UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString() };
#if NET7_0_OR_GREATER
        var response = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("rating", UriKind.Relative),
            Content = new StringContent(JsonConvert.SerializeObject(rating), Encoding.UTF8, "application/json"),
        });
        
        
        // Deserialize the input content but extract the Rating property. Be careful though, we might 404 if the user hasn't rated the module yet.
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogDebug("Failed to get user rating for module {ModuleId}", moduleMetadata.ModuleId);
            return null;
        }
        
        var ratingResponse = await Json.ToObjectAsync<RatingObject>(await response.Content.ReadAsStringAsync());
#else
        var response = _webClient.UploadString(new Uri(BaseUri, "rating"), WebRequestMethods.Http.Get,
            JsonConvert.SerializeObject(rating));
        var ratingResponse = await Json.ToObjectAsync<RatingObject>(response);
#endif
        
        _logger.LogDebug("Rating for {ModuleId} was {Rating}. Caching...", moduleMetadata.ModuleId, ratingResponse.Rating);
        _ratingCache[moduleMetadata.ModuleId] = ratingResponse.Rating;
        return ratingResponse.Rating;
    }

    public async Task SetMyRatingAsync(TrackingModuleMetadata moduleMetadata, int rating)
    {
        // Same format as get but we PUT this time
        var ratingObject = new RatingObject{UserId = _identityService.GetUniqueUserId(), ModuleId = moduleMetadata.ModuleId.ToString(), Rating = rating};

#if NET7_0_OR_GREATER
        var content = new StringContent(JsonConvert.SerializeObject(ratingObject), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync("rating", content);
        
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("Failed to set rating for {ModuleId} to {Rating}. Status code: {StatusCode}", moduleMetadata.ModuleId, rating, response.StatusCode);
            return;
        }
#else
        _webClient.UploadString(new Uri(BaseUri, "rating"), WebRequestMethods.Http.Put,
            JsonConvert.SerializeObject(ratingObject));
#endif
        
        _logger.LogDebug("Rating for {ModuleId} was set to {Rating}. Caching...", moduleMetadata.ModuleId, rating);
        _ratingCache[moduleMetadata.ModuleId] = rating;
    }

    public IEnumerable<InstallableTrackingModule> GetInstalledModules()
    {
        if (!Directory.Exists(Utils.CustomLibsDirectory))
        {
            Directory.CreateDirectory(Utils.CustomLibsDirectory);
        }

        // Check each folder in our CustomModulesDir folder and see if it has a module.json file.
        // If it does, deserialize it and add it to the list of installed modules.
        var installedModules = new List<InstallableTrackingModule>();
        var moduleFolders = Directory.GetDirectories(Utils.CustomLibsDirectory);
        foreach (var moduleFolder in moduleFolders)
        {
            var moduleJsonPath = Path.Combine(moduleFolder, "module.json");
            if (!File.Exists(moduleJsonPath))
            {
                continue;
            }

            var moduleJson = File.ReadAllText(moduleJsonPath);
            try
            {
                var module = JsonConvert.DeserializeObject<InstallableTrackingModule>(moduleJson);
                module!.AssemblyLoadPath = Path.Combine(moduleFolder, module.DllFileName);
                installedModules.Add(module);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to deserialize module.json for {ModuleFolder}", moduleFolder);
            }
        }

        return installedModules;
    } 
}