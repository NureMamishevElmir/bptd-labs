using Dh.Core;
using Dh.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomRegistry>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// Відкриті параметри груп (Q і A у hex) для браузера
app.MapGet("/api/groups", () => new
{
    small = Describe(DhGroup.Small),
    big = Describe(DhGroup.Rfc3526)
});
app.MapHub<ChatHub>("/chat");
app.Run();

static object Describe(DhGroup g) => new { name = g.Name, q = Hex.Of(g.Q), a = Hex.Of(g.A), bits = g.Bits };
