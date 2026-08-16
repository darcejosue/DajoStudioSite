using DajoStudio.UpdateServer.Data;
using DajoStudio.UpdateServer.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for 1GB upload limits
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_073_741_824; // 1 GB (1,073,741,824 bytes)
});

// Configure Form Options for large multipart file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 1_073_741_824; // 1 GB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Add EF Core SQLite Context
string dbPath = Path.Combine(builder.Environment.ContentRootPath, "updateserver.db");
builder.Services.AddDbContext<UpdateDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add Application Services
builder.Services.AddScoped<IUpdateStorageService, UpdateStorageService>();

// Add MVC & API Controllers
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure Database is created automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UpdateDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
