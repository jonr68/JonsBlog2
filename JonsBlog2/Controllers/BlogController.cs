using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using JonsBlog2.Models;
using Microsoft.AspNetCore.Authorization;
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
    public IActionResult GetListPage()
    {
        var listPagePath = Path.Combine(GetWebPagesDirectory(), "BlogListPage.html");

        if (!System.IO.File.Exists(listPagePath))
        {
            return NotFound();
        }

        return PhysicalFile(listPagePath, "text/html; charset=utf-8");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("write")]
    public IActionResult GetWritePage()
    {
        var writePagePath = Path.Combine(GetWebPagesDirectory(), "BlogWritePage.html");

        if (!System.IO.File.Exists(writePagePath))
        {
            return NotFound();
        }

        return PhysicalFile(writePagePath, "text/html; charset=utf-8");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Blog blog, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blog.Title) || string.IsNullOrWhiteSpace(blog.Content))
        {
            return BadRequest("Blog title and content are required.");
        }

        var postDate = ResolvePostDate(blog.Date);
        var blogsDirectory = GetBlogsDirectory();
        Directory.CreateDirectory(blogsDirectory);

        var slug = Slugify(blog.Title);
        var fileName = $"{slug}-{postDate:MM-dd-yyyy}.html";

        if (!IsSafeFileName(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        var blogsDirectoryFullPath = Path.GetFullPath(blogsDirectory);
        var blogFileFullPath = Path.GetFullPath(Path.Combine(blogsDirectoryFullPath, fileName));

        if (!blogFileFullPath.StartsWith(blogsDirectoryFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return BadRequest("Invalid file path.");
        }

        if (System.IO.File.Exists(blogFileFullPath))
        {
            return Conflict(new { message = "A blog file with this title and date already exists.", fileName });
        }

        var html = BuildBlogHtml(blog.Title, blog.Content, postDate, blog.Tags);
        await System.IO.File.WriteAllTextAsync(blogFileFullPath, html, Encoding.UTF8, cancellationToken);

        return CreatedAtAction(nameof(GetFile), new { fileName }, new { fileName });
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

    private static DateOnly ResolvePostDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var formats = new[] { "MM-dd-yyyy", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy" };
        if (DateOnly.TryParseExact(date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        if (DateOnly.TryParse(date, out parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static string Slugify(string title)
    {
        var builder = new StringBuilder(title.Length);
        var previousWasDash = false;

        foreach (var character in title.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (previousWasDash || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousWasDash = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "blog-post" : slug;
    }

    private static string BuildBlogHtml(string title, string content, DateOnly postDate, string[]? tags)
    {
        var safeTitle = HtmlEncoder.Default.Encode(title.Trim());
        var safeContent = HtmlEncoder.Default.Encode(content.Trim())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br />\n                        ", StringComparison.Ordinal);

        var safeTags = (tags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => HtmlEncoder.Default.Encode(tag.Trim()))
            .ToArray();

        var tagsLine = safeTags.Length == 0
            ? string.Empty
            : $"<p class=\"mt-4 text-center font-mono text-green-400\">Tags: {string.Join(" | ", safeTags)}</p>";

        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>{safeTitle}</title>
    <link rel="stylesheet" href="/css/output.css" />
</head>
<body class="h-screen bg-blue-700">
<div>
    <nav>
        <ul class="flex justify-center space-x-4 p-4 bg-gradient-to-tr from-amber-700 via-amber-400 to-yellow-200 rounded-lg text-amber-950 font-bold">
            <li><a href="#" class="font-bold text-black hover:text-gray-500">Home</a></li>
            <li><a href="#" class="font-bold text-black hover:text-gray-500">About</a></li>
            <li><a href="#" class="font-bold text-black hover:text-gray-500">Blog</a></li>
        </ul>
    </nav>
</div>
<div class="flex justify-center px-4 py-8">
    <div class="relative p-6 bg-[#2a2a2a] rounded-[40px] border-[12px] border-[#1a1a1a] shadow-[0_20px_50px_rgba(0,0,0,0.5),inset_0_2px_5px_rgba(255,255,255,0.1)]">
        <div class="rounded-[20px] overflow-hidden shadow-[inset_0_5px_15px_rgba(0,0,0,0.8),0_1px_1px_rgba(255,255,255,0.1)]">
            <div class="bg-zinc-950 p-10 min-h-[300px] relative">
                <div class="w-full max-w-3xl bg-black shadow-sm border border-slate-200 rounded-lg p-10">
                    <h1 class="font-mono text-green-500 [text-shadow:0_0_5px_rgba(34,197,94,0.8)] text-4xl mb-3 text-center">{safeTitle}</h1>
                    <p class="text-center text-green-400 mb-6">{postDate:MM-dd-yyyy}</p>
                    <p class="text-center font-mono text-green-500 [text-shadow:0_0_5px_rgba(34,197,94,0.8)]">{safeContent}</p>
                    {tagsLine}
                </div>
            </div>
        </div>
    </div>
</div>
</body>
</html>
""";
    }
}