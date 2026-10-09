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

app.MapGet("/api/search/", async (ProdajaFutbolokContext context, string search) =>
{
    if (search == null || string.IsNullOrWhiteSpace(search))
    {
        return Results.BadRequest(new { message = "Параметр обязателен" });
    }
    search = search.ToLower();
    var res = await context.Products.Where(p=> p.Title.Contains(search)).ToListAsync();
    return Results.Json(res);

}).AllowAnonymous();

app.MapGet("/api/orders_withstatus/", async (ProdajaFutbolokContext context, string stutus) =>
{
    if (stutus == null || string.IsNullOrWhiteSpace(stutus))
    {
        return Results.BadRequest(new { message = "Параметр query обязателен" });
    }
    var orders = await db.Orders.OrderStatus.Where(o => o.StatusName == status).ToListAsync();

    return Results.Json(orders);

}).RequireAuthorization();

app.MapPost("/api/auth/register", async (ProdajaFutbolokContext context, RegisterRequest request) =>
{

    if (string.IsNullOrWhiteSpace(request.Login))
        return Results.BadRequest(new { message = "Логин обязателен" });

    if (string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { message = "Пароль обязателен" });

    if (string.IsNullOrWhiteSpace(request.Phone))
        return Results.BadRequest(new { message = "Телефон обязателен" });


    var exists = await db.Users.AnyAsync(u => u.Login == request.Login);
    if (exists)  return Results.Conflict(new { message = "Логин уже занят" });


    var hasher = new PasswordHasher<User>();

    var user = new User
    {
        Login = request.Login,
        PhoneNumber = request.Phone,               
        DeliveryAddress = request.Address,         
        RoleId = 2,                                
        CreatedAt = DateTime.UtcNow
    };
    user.PasswordHash = hasher.HashPassword(user, request.Password);

    context.Users.Add(user);
    await context.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", new
    {
        user.Id,
        user.Login,
        user.PhoneNumber,
        user.DeliveryAddress,
        user.RoleId
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
