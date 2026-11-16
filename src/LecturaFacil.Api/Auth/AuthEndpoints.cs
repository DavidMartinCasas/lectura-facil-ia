// SPDX-License-Identifier: Apache-2.0
using System.Security.Claims;
using FluentValidation;
using LecturaFacil.Api.Security;
using LecturaFacil.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace LecturaFacil.Api.Auth;

/// <summary>
/// <c>POST /api/auth/login</c>, <c>POST /api/auth/logout</c> y <c>GET /api/auth/me</c> (US-15). La sesión es una cookie
/// HttpOnly del backend; el frontend no guarda ningún token. Un fallo de inicio de sesión responde siempre lo mismo, sin
/// decir si falla el correo, la contraseña o si la cuenta está bloqueada (OWASP API2).
/// </summary>
internal static partial class AuthEndpoints
{
    public const string Path = "/api/auth";
    public const string LoginRateLimitPolicy = "auth-login";
    public const string InvalidCredentialsCode = "auth.invalid_credentials";
    public const string InvalidCredentialsMessage = "Correo o contraseña incorrectos.";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Path).WithTags("Sesión");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimitPolicy)
            .Accepts<LoginRequest>("application/json")
            .Produces<CurrentUserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("Login")
            .WithSummary("Inicia sesión con correo y contraseña y deja la cookie de sesión.")
            .WithDescription(
                "Necesita la cabecera X-XSRF-TOKEN con el valor de la cookie XSRF-TOKEN, que deja cualquier GET a /api. "
                + "Tras varios fallos seguidos la cuenta se bloquea un tiempo; la respuesta no lo distingue de un fallo normal.");

        group.MapPost("/logout", LogoutAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("Logout")
            .WithSummary("Cierra la sesión: la cookie deja de valer también en el servidor.");

        group.MapGet("/me", GetCurrentUser)
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetCurrentUser")
            .WithSummary("Usuario con sesión. 401 si no hay sesión.");

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        SignInManager<ApplicationUser> signInManager,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var email = request.Email!.Trim();
        var result = await signInManager.PasswordSignInAsync(email, request.Password!, isPersistent: false, lockoutOnFailure: true);
        var logger = loggerFactory.CreateLogger(typeof(AuthEndpoints).FullName!);
        if (!result.Succeeded)
        {
            // Ni el correo ni la contraseña en el log: solo si la cuenta ha quedado bloqueada.
            LoginFailed(logger, result.IsLockedOut);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: InvalidCredentialsMessage,
                extensions: new Dictionary<string, object?> { ["code"] = InvalidCredentialsCode });
        }

        var user = await signInManager.UserManager.FindByNameAsync(email);
        context.User = await signInManager.CreateUserPrincipalAsync(user!);
        AntiforgeryMiddleware.IssueToken(context);
        LoginSucceeded(logger, user!.Id);
        return TypedResults.Ok(new CurrentUserResponse(user.Email!));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager, HttpContext context)
    {
        // Cambiar el sello de seguridad invalida en el servidor cualquier cookie emitida antes para este usuario.
        if (await signInManager.UserManager.GetUserAsync(context.User) is { } user)
        {
            await signInManager.UserManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        AntiforgeryMiddleware.IssueToken(context);
        return TypedResults.NoContent();
    }

    private static IResult GetCurrentUser(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserResponse(user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? string.Empty));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inicio de sesión fallido. Cuenta bloqueada: {LockedOut}.")]
    private static partial void LoginFailed(ILogger logger, bool lockedOut);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inicio de sesión del usuario {UserId}.")]
    private static partial void LoginSucceeded(ILogger logger, Guid userId);
}
