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
    public class BinanceWebSocketService
    {
        private readonly BinanceRestClient _client;
        private readonly BinanceSocketClient _socketClient;
        private int batchSize = 20;

        public BinanceWebSocketService()
        {
            _client = new BinanceRestClient();
            _socketClient = new BinanceSocketClient();
        }
        static void WriteToTxtFile(DataEvent<IBinanceFuturesMarkPrice> data)
        {
            string filePath = "binance.txt"; // TXT dosya adı

            string fundingRatePercent = (data.Data.FundingRate * 100)+ "%";

            string logEntry = $"{DateTime.UtcNow}: {data.Data.Symbol} - Mark Price: {data.Data.MarkPrice}, Funding Rate: {fundingRatePercent}\n";

            // Veriyi TXT dosyasına ekle
            File.AppendAllText(filePath, logEntry);
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
                .SubscribeToMarkPriceUpdatesAsync(symbols, 1000, data =>
                {
                    Console.WriteLine($"Symbol: {data.Data.Symbol} | Funding Rate: {data.Data.FundingRate} | Mark Price: {data.Data.MarkPrice}");


                    string filePath = "binance.txt";
                    string fundingRatePercent = (data.Data.FundingRate * 100) + "%";

                    string logEntry = $"{DateTime.UtcNow}: {data.Data.Symbol} - Mark Price: {data.Data.MarkPrice}, Funding Rate: {fundingRatePercent}\n";
                    File.AppendAllText(filePath, logEntry);

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
