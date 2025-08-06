using Microsoft.EntityFrameworkCore;
using UrlShortener.UrlService.Data;
using UrlShortener.UrlService.Services;
using UrlShortener.Common.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Entity Framework
builder.Services.AddDbContext<UrlDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Caching services
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "UrlShortener";
        });
        Console.WriteLine("Redis cache configured successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to configure Redis: {ex.Message}. Falling back to in-memory cache.");
        builder.Services.AddMemoryCache();
        // Add a fallback IDistributedCache implementation using IMemoryCache
        builder.Services.AddSingleton<IDistributedCache, MemoryCacheDistributedCache>();
    }
}
else
{
    Console.WriteLine("No Redis connection string found. Using in-memory cache.");
    builder.Services.AddMemoryCache();
    // Add a fallback IDistributedCache implementation using IMemoryCache
    builder.Services.AddSingleton<IDistributedCache, MemoryCacheDistributedCache>();
}

// Add cache service
builder.Services.AddScoped<ICacheService, CacheService>();

// Add application services
builder.Services.AddScoped<IUrlService, UrlShortener.UrlService.Services.UrlService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "URL Service API v1");
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowFrontend");

app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "URL", Timestamp = DateTime.UtcNow }));

app.Run();

// Fallback IDistributedCache implementation using IMemoryCache
public class MemoryCacheDistributedCache : IDistributedCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheDistributedCache> _logger;

    public MemoryCacheDistributedCache(IMemoryCache memoryCache, ILogger<MemoryCacheDistributedCache> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public byte[]? Get(string key)
    {
        try
        {
            return _memoryCache.Get<byte[]>(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from memory cache for key: {Key}", key);
            return null;
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        return await Task.FromResult(Get(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        try
        {
            var memoryCacheOptions = new MemoryCacheEntryOptions();
            
            if (options.AbsoluteExpiration.HasValue)
                memoryCacheOptions.AbsoluteExpiration = options.AbsoluteExpiration;
            if (options.SlidingExpiration.HasValue)
                memoryCacheOptions.SlidingExpiration = options.SlidingExpiration;
            
            _memoryCache.Set(key, value, memoryCacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in memory cache for key: {Key}", key);
        }
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        await Task.Run(() => Set(key, value, options), token);
    }

    public void Refresh(string key)
    {
        try
        {
            var value = _memoryCache.Get<byte[]>(key);
            if (value != null)
            {
                _memoryCache.Set(key, value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing value in memory cache for key: {Key}", key);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        await Task.Run(() => Refresh(key), token);
    }

    public void Remove(string key)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from memory cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await Task.Run(() => Remove(key), token);
    }
}
