using System.ComponentModel.DataAnnotations;
namespace AiGateway.Dtos;

public record CreateUserRequest(
    [Required, StringLength(150, MinimumLength = 2)] string UserName,
    [Required, EmailAddress, StringLength(150)] string UserEmail,
    [Required, StringLength(72, MinimumLength = 12)] string Password,
    int[]? GroupIds = null);

public record UpdateUserRequest(
    [Required, StringLength(150, MinimumLength = 2)] string UserName,
    [Required, EmailAddress, StringLength(150)] string UserEmail,
    bool IsActive,
    int[] GroupIds,
    [StringLength(72, MinimumLength = 12)] string? Password = null);

public record GroupRequest([Required, StringLength(100, MinimumLength = 2)] string Name, string[] Permissions);
