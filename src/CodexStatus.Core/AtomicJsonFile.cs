using System.Text.Json;

namespace CodexStatus.Core;

public static class AtomicJsonFile
{
    public static void Write<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Path must include a directory.", nameof(path));
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, options ?? CodexJson.Indented);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                catch (IOException)
                {
                    File.Move(tempPath, path, overwrite: true);
                }
                catch (UnauthorizedAccessException)
                {
                    File.Move(tempPath, path, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static T? Read<T>(string path, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, options ?? CodexJson.Default);
    }
}
