﻿using System.Text;
using Newtonsoft.Json;
using VRCFaceTracking.Core.Contracts.Services;

namespace VRCFaceTracking.Core.Services;

public class FileService : IFileService
{
    public T Read<T>(string folderPath, string fileName)
    {
        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public async Task Save<T>(string folderPath, string fileName, T content)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonConvert.SerializeObject(content);
#if NET7_0_OR_GREATER
        await File.WriteAllTextAsync(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
#else
        File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
        await Task.CompletedTask;
#endif
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
