using Agender.Shared.Abstractions;

namespace Agender;

public sealed class AndroidFileSaver : INativeFileSaver
{
    public bool CanCreatePdf => false;

    public async Task SaveAsync(string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        string folder = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);

        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(path, contentType)
        }));
    }
}
