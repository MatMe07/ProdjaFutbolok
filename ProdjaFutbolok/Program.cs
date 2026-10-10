using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProdjaFutbolok.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ProdjaFutbolok;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProdajaFutbolokContext>();
// Add services to the container.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

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
app.MapGet("/api/getProducts", async (ProdajaFutbolokContext contex) =>
{
    var prods = await contex.Products.ToListAsync();
    return Results.Json(prods);
}).AllowAnonymous();

app.MapPost("/api/newOrder", async (ProdajaFutbolokContext context, List<OrderItemProd> orderItemProds) =>
{
    var order = new Order
    {
        UserId = 2,
        StatusId=1,
        CreatedAt=DateTime.Now
    };
    context.Orders.Add(order);
    await context.SaveChangesAsync();
    foreach(var prod in orderItemProds)
    {
        var findProd = await context.Products.FirstOrDefaultAsync(p => p.Id == prod.ProdId);
        if (findProd == null)
        {
            return Results.NotFound($"Продукта с id = {prod.ProdId} нет!");
        }
        var findSize = await context.Sizes.FirstOrDefaultAsync(p => p.Id == prod.SizeId);
        if (findSize == null)
        {
            return Results.NotFound($"Размера с id = {prod.ProdId} нет!");
        }
        
        var price = findProd.Price;
        var new_ordProd = new OrderItem
        {
            OrderId = order.Id,
            ProductId = prod.ProdId,
            SizeId = prod.SizeId,
            Quantity = prod.Quantity,
            PriceAtPurchase = price,
        };
        context.OrderItems.Add(new_ordProd); 
    }
    await context.SaveChangesAsync();
    return Results.Json(order) ;
}).RequireAuthorization();

app.MapGet("/api/getUserOrders/{UserId}", async (ProdajaFutbolokContext context, int UserId) =>
{
    var user = await context.Users.FirstOrDefaultAsync(us => us.Id == UserId);
    if (user == null)
    {
        return Results.NotFound("Такого пользователя нет!") ;

    }
    return Results.Json(context.Orders.Where(o => o.UserId == UserId));
}).RequireAuthorization();


app.MapPatch("/api/changeOrderStatus", async (ProdajaFutbolokContext context, int OrderId, int StatusId) =>
{
    var order = await context.Orders.FirstOrDefaultAsync(o=>o.Id == OrderId);
    if (order == null)
    {
        return Results.NotFound("Такого заказа нет!") ;
    }
    if (await context.OrderStatuses.FirstOrDefaultAsync(s=>s.Id == StatusId) == null)
    {
        return Results.NotFound("Такого статуса нет!") ;
    }
    order.StatusId = StatusId;
    await context.SaveChangesAsync();
    return Results.Ok(order);
}).RequireAuthorization(r => r.RequireAuthenticatedUser().RequireRole("1"));


app.MapPatch("/api/removeProduct/{ProductId}", async (ProdajaFutbolokContext context, int ProductId) =>
{
    var product = await context.Products.FirstOrDefaultAsync(o=>o.Id == ProductId);
    if (product == null)
    {
        return Results.NotFound("Такого продукта нет!");
    }
    product.IsAvailable = false;
    await context.SaveChangesAsync();
    return Results.Ok($"Товар (#{ProductId}){product.Title} убран");

}).RequireAuthorization(r => r.RequireAuthenticatedUser().RequireRole("1"));

app.MapPatch("/api/returnProduct/{ProductId}", async (ProdajaFutbolokContext context, int ProductId) =>
{
    var product = await context.Products.FirstOrDefaultAsync(o=>o.Id == ProductId);
    if (product == null)
    {
        return Results.NotFound("Такого продукта нет!");
    }
    product.IsAvailable = true;
    await context.SaveChangesAsync();
    return Results.Ok($"Товар (#{ProductId}){product.Title} возвращен");

}).RequireAuthorization(r => r.RequireAuthenticatedUser().RequireRole("1"));

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

app.MapGet("/api/search", async (ProdajaFutbolokContext context, string search) =>
{
    if (search == null || string.IsNullOrWhiteSpace(search))
    {
        return Results.BadRequest(new { message = "Параметр обязателен" });
    }
    search = search.ToLower();
    var res = await context.Products.Where(p=> p.Title.Contains(search)).ToListAsync();
    return Results.Json(res);

}).AllowAnonymous();

app.MapGet("/api/orders_withstatus", async (ProdajaFutbolokContext context, string status) =>
{
    if (status == null || string.IsNullOrWhiteSpace(status))
    {
        return Results.BadRequest(new { message = "Параметр обязателен" });
    }
    var orders = await context.Orders.Where(o => o.Status.StatusName == status).ToListAsync();

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


    var exists = await context.Users.AnyAsync(u => u.Login == request.Login);
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

app.MapPut("/api/inventory", async (ProdajaFutbolokContext context, InvetoryRequest request) =>
{
    if(request.quantity < 0)
    {
        return Results.BadRequest(new { message = "Отрицательное количество" });
    }

    var inventory = await context.ProductInventories.FirstOrDefaultAsync(p => p.ProductId == request.prodID && p.SizeId == request.sizeID);

    if (inventory == null) {
        return Results.NotFound(new { message = "Не найдено" });

    }

    inventory.StockQuantity = request.quantity;
    await context.SaveChangesAsync();

    return Results.Json(inventory);

}).RequireAuthorization(r => r.RequireAuthenticatedUser().RequireRole("1"));


app.Run();

string CreateToken(User user)
{
    var claims = new[]
    {
        new Claim("sub", user.Id.ToString()),
        new Claim("name", user.Login),
        new Claim("role", user.RoleId.ToString())
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
