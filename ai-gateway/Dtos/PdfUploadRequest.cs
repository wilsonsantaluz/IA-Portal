using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

public class PdfUploadRequest
{
    [Required]
    public IFormFile File { get; set; }

    // public string Model { get; set; } = "deepseek-r1:8b";
    public string Model { get; set; } = "deepseek-chat";
}
