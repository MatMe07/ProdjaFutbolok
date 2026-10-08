using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ProdjaFutbolok.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ProdjaFutbolok;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProdajaFutbolokContext>();
// Add services to the container.

var hasher = new PasswordHasher<User>();

var key = new SymmetricSecurityKey(
    RandomNumberGenerator.GetBytes(32));

var validation = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "JwtClothes",

    ValidateAudience = true,
    ValidAudience = "JwtClothesApi",

    ValidateIssuerSigningKey = true,
    IssuerSigningKey = key,

    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,

    NameClaimType = "name",
    RoleClaimType = "role"
};

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = validation;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGet("/gets", (ProdajaFutbolokContext contex) =>
{
    return contex.Products;
});

app.MapPost("/api/auth/login", (ProdajaFutbolokContext context, LoginRequest request) =>
{
    var account = context.Users.FirstOrDefault(
        a => a.Login == request.Login);

    if (account == null ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.Challenge();

    var result = hasher.VerifyHashedPassword(
        account, account.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Challenge();

    return Results.Ok(new
    {
        access_token = CreateToken(account),
        token_type = "Bearer"
    });
}).AllowAnonymous();

app.Run();

string CreateToken(User user)
{
    var claims = new[]
    {
        new Claim("sub", user.Id.ToString()),
        new Claim("name", user.Login),
        new Claim("role", user.Role.RoleName)
    };

    var token = new JwtSecurityToken(
        issuer: "JwtClothes",
        audience: "JwtClothesApi",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}
