namespace CodeNexus.API.Models.Requests;

public class UpdateResourceRequest
{
    public IFormFile? File { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}
