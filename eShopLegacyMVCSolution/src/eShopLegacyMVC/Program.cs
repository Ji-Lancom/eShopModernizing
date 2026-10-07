using Autofac;
using Autofac.Extensions.DependencyInjection;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Use Autofac as DI container
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

// Add MVC services
builder.Services.AddControllersWithViews();

// Configure EF Core
bool useMockData = builder.Configuration.GetValue<bool>("UseMockData");
if (!useMockData)
{
    string connectionString = builder.Configuration.GetConnectionString("CatalogDBContext")
        ?? throw new InvalidOperationException("Connection string 'CatalogDBContext' not found.");
    builder.Services.AddDbContext<CatalogDBContext>(options =>
        options.UseSqlServer(connectionString));
}

// Configure session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure Autofac container
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule(new ApplicationModule(useMockData));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Catalog/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.MapControllers();

// Initialize database on startup
if (!useMockData)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var indexGenerator = scope.ServiceProvider.GetService<CatalogItemHiLoGenerator>()
        ?? new CatalogItemHiLoGenerator();
    bool useCustomizationData = config.GetValue<bool>("UseCustomizationData");
    var initializer = new CatalogDBInitializer(indexGenerator, env.ContentRootPath, useCustomizationData);
    initializer.Initialize(context);
}

app.Run();
