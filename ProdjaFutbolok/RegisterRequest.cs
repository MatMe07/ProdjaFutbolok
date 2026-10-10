namespace ProdjaFutbolok
{
    public record RegisterRequest(
    string Login,
    string Password,
    string Phone,
    string? Address);
}
