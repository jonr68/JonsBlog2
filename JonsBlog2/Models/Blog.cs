namespace JonsBlog2.Models;

public class Blog
{
    
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();

}