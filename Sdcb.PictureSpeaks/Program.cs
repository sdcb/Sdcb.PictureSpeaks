using Microsoft.EntityFrameworkCore;
using Sdcb.PictureSpeaks.Hubs;
using Sdcb.PictureSpeaks.Services.DALL_E3;
using Sdcb.PictureSpeaks.Services.DB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<DallE3Client>();
builder.Services.AddSignalR();
builder.Services.AddDbContext<Storage>(options => options.UseSqlite("Data Source=storage.db"));
builder.Services.AddTransient<LobbyRepository>();

builder.Services.AddMvc().AddRazorRuntimeCompilation();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapHub<MainHub>("/mainHub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
using (IServiceScope scope = scopeFactory.CreateScope())
{
    using Storage db = scope.ServiceProvider.GetRequiredService<Storage>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
