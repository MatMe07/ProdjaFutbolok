using ProdjaFutbolok.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProdajaFutbolokContext>();
// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/getProducts", (ProdajaFutbolokContext contex) =>
{
    return contex.Products;
});


app.Run();
