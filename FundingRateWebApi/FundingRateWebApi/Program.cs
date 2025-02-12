using FundingRateWebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Servisi DI Container'a ekliyoruz
builder.Services.AddSingleton<BinanceWebSocketService>();

builder.Services.AddControllers();

var app = builder.Build();

// WebSocket servisini başlatıyoruz
var binanceWebSocketService = app.Services.GetRequiredService<BinanceWebSocketService>();
await binanceWebSocketService.SubscribeToAllFundingRatesAsync();

app.MapControllers();

app.Run();