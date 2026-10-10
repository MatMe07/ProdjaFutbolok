using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProdjaFutbolok.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ProdjaFutbolok;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
});

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
            return Results.NotFound($"Ïðîäóêòà ñ id = {prod.ProdId} íåò!");
        }
        var findSize = await context.Sizes.FirstOrDefaultAsync(p => p.Id == prod.SizeId);
        if (findSize == null)
        {
            return Results.NotFound($"Ðàçìåðà ñ id = {prod.ProdId} íåò!");
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
});

app.MapGet("/api/getUserOrders/{UserId}", (ProdajaFutbolokContext context, int UserId) =>
{
    return context.Orders.Where(o => o.UserId == UserId);
});


app.MapPatch("/api/changeOrderStatus", async (ProdajaFutbolokContext context, int OrderId, int StatusId) =>
{
    var order = await context.Orders.FirstOrDefaultAsync(o=>o.Id == OrderId);
    if (order == null)
    {
        return Results.NotFound("Òàêîãî çàêàçà íåò!") ;
    }
    if (await context.OrderStatuses.FirstOrDefaultAsync(s=>s.Id == StatusId) == null)
    {
        return Results.NotFound("Òàêîãî ñòàòóñà íåò!") ;
    }
    order.StatusId = StatusId;
    await context.SaveChangesAsync();
    return Results.Ok(order);
});


app.MapPatch("/api/removeProduct/{ProductId}", async (ProdajaFutbolokContext context, int ProductId) =>
{
    var product = await context.Products.FirstOrDefaultAsync(o=>o.Id == ProductId);
    if (product == null)
    {
        return Results.NotFound("Òàêîãî ïðîäóêòà íåò!");
    }
    product.IsAvailable = false;
    await context.SaveChangesAsync();
    return Results.Ok($"Òîâàð (#{ProductId}){product.Title} óáðàí");

});

app.MapPatch("/api/returnProduct/{ProductId}", async (ProdajaFutbolokContext context, int ProductId) =>
{
    var product = await context.Products.FirstOrDefaultAsync(o=>o.Id == ProductId);
    if (product == null)
    {
        return Results.NotFound("Òàêîãî ïðîäóêòà íåò!");
    }
    product.IsAvailable = true;
    await context.SaveChangesAsync();
    return Results.Ok($"Òîâàð (#{ProductId}){product.Title} âîçâðàùåí");

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

app.MapGet("/api/search", async (ProdajaFutbolokContext context, string search) =>
{
    if (search == null || string.IsNullOrWhiteSpace(search))
    {
        return Results.BadRequest(new { message = "Ïàðàìåòð îáÿçàòåëåí" });
    }
    search = search.ToLower();
    var res = await context.Products.Where(p=> p.Title.Contains(search)).ToListAsync();
    return Results.Json(res);

}).AllowAnonymous();

app.MapGet("/api/orders_withstatus", async (ProdajaFutbolokContext context, string status) =>
{
    if (status == null || string.IsNullOrWhiteSpace(status))
    {
        return Results.BadRequest(new { message = "Ïàðàìåòð îáÿçàòåëåí" });
    }
    var orders = await context.Orders.Where(o => o.Status.StatusName == status).ToListAsync();

    return Results.Json(orders);

}).RequireAuthorization();

app.MapPost("/api/auth/register", async (ProdajaFutbolokContext context, RegisterRequest request) =>
{

    if (string.IsNullOrWhiteSpace(request.Login))
        return Results.BadRequest(new { message = "Ëîãèí îáÿçàòåëåí" });

    if (string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { message = "Ïàðîëü îáÿçàòåëåí" });

    if (string.IsNullOrWhiteSpace(request.Phone))
        return Results.BadRequest(new { message = "Òåëåôîí îáÿçàòåëåí" });


    var exists = await context.Users.AnyAsync(u => u.Login == request.Login);
    if (exists)  return Results.Conflict(new { message = "Ëîãèí óæå çàíÿò" });


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
        return Results.BadRequest(new { message = "Îòðèöàòåëüíîå êîëè÷åñòâî" });
    }

    var inventory = await context.ProductInventories.FirstOrDefaultAsync(p => p.ProductId == request.prodID && p.SizeId == request.sizeID);

    if (inventory == null) {
        return Results.NotFound(new { message = "Íå íàéäåíî" });

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
