namespace AiGateway.Dtos
{

    public record CreateUserRequest(
string UserName,
string UserEmail,
string Password
     );

    public record UpdateUserRequest(
        string UserName,
        string UserEmail,
        bool IsActive
    );
}

