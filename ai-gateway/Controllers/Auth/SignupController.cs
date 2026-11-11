using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portal.AI.Gateway.Infrastructure;

[ApiController, AllowAnonymous, Route("api/auth/signup"), EnableRateLimiting("signup")]
public sealed class SignupController(AtlasSignupService signup) : ControllerBase
{
    [HttpGet("status"), EnableRateLimiting("signup-status")]
    public object Status()=>new{enabled=signup.Enabled};
    [HttpPost]
    public async Task<IActionResult> Register(SignupInput input,CancellationToken ct) {
        await signup.Register(input,ct);
        return Accepted(new{message="Se o e-mail estiver apto para cadastro, você receberá um link de confirmação. Verifique também o spam."});
    }
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(SignupConfirmation input,CancellationToken ct) {
        await signup.Confirm(input.Token,ct);return Ok(new{message="E-mail confirmado. Entre com sua senha para acessar o IA-Portal."});
    }
}
