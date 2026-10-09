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
app.MapGet("/getProducts", (ProdajaFutbolokContext contex) =>
{
    return contex.Products.FirstOrDefault();
});

app.MapPost("/newOrder", async (ProdajaFutbolokContext context, List<OrderItemProd> orderItemProds) =>
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
        var price = context.Products.FirstOrDefault(p => p.Id == prod.ProdId).Price;

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
    return order;
});

app.Run();
