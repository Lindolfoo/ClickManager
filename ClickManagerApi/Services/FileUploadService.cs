namespace ClickManagerApi.Services;

public interface IFileUploadService
{
    Task<string> UploadAsync(IFormFile file, string subfolder);
    void Delete(string url);
}

public class FileUploadService : IFileUploadService
{
    private readonly IWebHostEnvironment  _env;
    private readonly IHttpContextAccessor _http;

    private static readonly HashSet<string> Allowed = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    public FileUploadService(IWebHostEnvironment env, IHttpContextAccessor http)
    {
        _env  = env;
        _http = http;
    }

    public async Task<string> UploadAsync(IFormFile file, string subfolder)
    {
        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Arquivo excede 10 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext))
            throw new InvalidOperationException("Formato não permitido. Use jpg, png, webp ou gif.");

        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir  = Path.Combine(root, "uploads", subfolder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var req = _http.HttpContext!.Request;
        return $"{req.Scheme}://{req.Host}/uploads/{subfolder}/{fileName}";
    }

    public void Delete(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        var relative = uri.AbsolutePath.TrimStart('/');
        var root     = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var fullPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }
}
