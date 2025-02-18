namespace FundingRateWebApi
{
    using Binance.Net.Clients;
    using Binance.Net.Interfaces;
    using Binance.Net.Objects;
    using Binance.Net.Objects.Models.Futures;
    using CryptoExchange.Net.Authentication;
    using CryptoExchange.Net.Objects.Sockets;
    using CryptoExchange.Net.Sockets;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Telegram.Bot;
    using Telegram.Bot.Types;

    public class BinanceWebSocketService
    {
        private readonly BinanceRestClient _client;
        private readonly BinanceSocketClient _socketClient;
        private int batchSize = 20;
        private static readonly string botToken = "7938765330:AAFC6-bpOiffLaa8iSQwpzl0h3FR_yYT4s4";
        private static readonly string chatId = "7250151162";
        decimal negativeThreshold = -2m;
        private List<FundingRateRecord> fundingRateRecords = new List<FundingRateRecord>();

        public BinanceWebSocketService()
        {
            _client = new BinanceRestClient();
            _socketClient = new BinanceSocketClient();
        }
        public async Task sendTelegramMessage(string message)
        {
            var botClient = new TelegramBotClient(botToken);
            await botClient.SendTextMessageAsync(chatId, message);
        }
        public async Task SubscribeToAllFundingRatesAsync()
        {
            var symbolsResult = await _client.UsdFuturesApi.ExchangeData.GetTickersAsync();

            if (!symbolsResult.Success)
            {
                Console.WriteLine($"Symbol listesi alınamadı: {symbolsResult.Error}");
                return;
            }

            var symbols = symbolsResult.Data
                .Select(ticker => ticker.Symbol.ToLower())
                .Distinct()
                .ToList();

            Console.WriteLine($"Toplam {symbols.Count} sembol bulundu.");

            await sendTelegramMessage("Start Bot");

            var symbolBatches = symbols.Select((symbol, index) => new { symbol, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.symbol).ToList())
                .ToList();

            foreach (var batch in symbolBatches)
            {
                await SubscribeToBatch(batch);
            }
        }
        private async Task SubscribeToBatch(List<string> symbols)
        {
            var subscriptionResult = await _socketClient.UsdFuturesApi.ExchangeData
                .SubscribeToMarkPriceUpdatesAsync(symbols, 1000, async data =>
                {
                    Console.WriteLine($"Symbol: {data.Data.Symbol} | Funding Rate: {data.Data.FundingRate} | Mark Price: {data.Data.MarkPrice}");

                    var fundingRate = data.Data.FundingRate; // Funding Rate'i al
                    decimal fundingRatePercentage = (decimal)fundingRate * 100; // Yüzdesel olarak formatla
                    var dateTime = data.Data.EventTime.ToString("yyyy-MM-dd HH:mm:ss"); // Tarihi formatla
                    var symbol = data.Data.Symbol;

                    if(fundingRatePercentage <= negativeThreshold)
                    {
                        var exist = fundingRateRecords.Any(r => r.Symbol == symbol);
                       if (!exist)
                        {
                            var record = new FundingRateRecord
                            {
                                Timestamp = DateTime.Now,  // Şu anki tarih ve saat
                                FundingRate = fundingRatePercentage,  // Yüzde formatında
                                Price = data.Data.MarkPrice,
                                Symbol = symbol
                            };

                            fundingRateRecords.Add(record);

                        string message = $"📅 *Zaman:* `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`\n" +
                        $"💰 *Fiyat:* `{data.Data.MarkPrice} USDT`\n" +
                        $"🔄 *Funding Rate:* `{fundingRatePercentage.ToString("F4")} %`\n" +
                        $"🔹 *Sembol:* `{symbol}`";

                            await sendTelegramMessage(message);


                        }
                    }
                });

            if (!subscriptionResult.Success)
            {
                Console.WriteLine($"Abonelik başarısız: {subscriptionResult.Error}");
                await SubscribeToAllFundingRatesAsync();
            }
            else
            {
                Console.WriteLine($"Abonelik başarılı: {string.Join(", ", symbols)}");

                // Bağlantı kaybı durumunda yeniden bağlanma
                subscriptionResult.Data.ConnectionLost += async () =>
                {
                    Console.WriteLine($"Bağlantı kayboldu: {string.Join(", ", symbols)}. Yeniden bağlanılıyor...");
                    await SubscribeToAllFundingRatesAsync();
                };

                // Bağlantı geri geldiğinde bildirim
                subscriptionResult.Data.ConnectionRestored += (reconnectTime) =>
                {
                    Console.WriteLine($"Bağlantı yeniden sağlandı: {string.Join(", ", symbols)}. Bağlantı kesilme süresi: {reconnectTime.TotalSeconds} saniye.");
                };
            }
        }
    }
}
public class FundingRateRecord
{
    public DateTime Timestamp { get; set; }
    public decimal FundingRate { get; set; }
    public decimal Price { get; set; }

    public string Symbol { get; set; }
}