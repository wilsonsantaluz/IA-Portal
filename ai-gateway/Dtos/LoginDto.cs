using System.ComponentModel.DataAnnotations;

namespace AiGateway.Dtos
{

    public record LoginRequest(
    [Required, EmailAddress, StringLength(150)] string Email,
    [Required, StringLength(72)] string Password
);

    public record LoginResponse(
        int UserId,
        string UserName,
        string Email,
        string Token,
        string[] Roles,
        string[] Permissions
    );

}
