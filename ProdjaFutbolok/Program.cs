using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using ProdjaFutbolok;
using ProdjaFutbolok.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProdajaFutbolokContext>();
// Add services to the container.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

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
        return Results.NotFound("Такого заказа нет!") ;
    }
    if (await context.OrderStatuses.FirstOrDefaultAsync(s=>s.Id == StatusId) == null)
    {
        return Results.NotFound("Такого статуса нет!") ;
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
        return Results.NotFound("Такого продукта нет!");
    }
    product.IsAvailable = false;
    await context.SaveChangesAsync();
    return Results.Ok($"Товар (#{ProductId}){product.Title} убран");

});

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

});

app.Run();
