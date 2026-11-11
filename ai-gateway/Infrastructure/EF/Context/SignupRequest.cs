namespace Portal.AI.Gateway.Infrastructure;

public sealed class SignupRequest
{
    public string TokenHash { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
