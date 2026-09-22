namespace AiGateway.Dtos
{

    public record LoginRequest(
    string Email,
    string Password
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
