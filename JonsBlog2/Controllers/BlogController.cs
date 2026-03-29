using Microsoft.AspNetCore.Mvc;

namespace JonsBlog2.Controllers;

[ApiController]
[Route("blogs")]
public class BlogController : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".html", ".htm"];
    private readonly IWebHostEnvironment _environment;

    public BlogController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    [HttpGet("files")]
    public ActionResult<IEnumerable<string>> GetFiles()
    {
        var blogsDirectory = GetBlogsDirectory();

        if (!Directory.Exists(blogsDirectory))
        {
            return Ok(Array.Empty<string>());
        }

        var fileNames = Directory.EnumerateFiles(blogsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Where(IsAllowedBlogFileName)
            .Cast<string>()
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(fileNames);
    }

    [HttpGet("files/{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        if (!IsSafeFileName(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        var blogsDirectory = GetBlogsDirectory();

        if (!Directory.Exists(blogsDirectory))
        {
            return NotFound();
        }

        var blogsDirectoryFullPath = Path.GetFullPath(blogsDirectory);
        var blogFileFullPath = Path.GetFullPath(Path.Combine(blogsDirectoryFullPath, fileName));

        if (!blogFileFullPath.StartsWith(blogsDirectoryFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return BadRequest("Invalid file path.");
        }

        if (!System.IO.File.Exists(blogFileFullPath))
        {
            return NotFound();
        }

        return PhysicalFile(blogFileFullPath, "text/html; charset=utf-8");
    }

    [HttpGet("list")]
    public IActionResult GetListPage()
    {
        var listPagePath = Path.Combine(GetWebPagesDirectory(), "BlogListPage.html");

        if (!System.IO.File.Exists(listPagePath))
        {
            return NotFound();
        }

        return PhysicalFile(listPagePath, "text/html; charset=utf-8");
    }

    private string GetBlogsDirectory()
    {
        return Path.Combine(_environment.ContentRootPath, "Blogs");
    }

    private string GetWebPagesDirectory()
    {
        return Path.Combine(_environment.ContentRootPath, "WebPages");
    }

    private static bool IsSafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        return IsAllowedBlogFileName(fileName);
    }

    private static bool IsAllowedBlogFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}