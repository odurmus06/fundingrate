namespace FundingRateWebApi
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class FundingRate
    {
        [Key]
        public int Id { get; set; }  // Otomatik artan ID

        [Required]
        public string Symbol { get; set; }  // Coin sembolü (örn: BTCUSDT)

        public decimal Rate { get; set; }  // Funding rate değeri

        public decimal MarkPrice { get; set; }  // Mark fiyatı

        public DateTime Timestamp { get; set; }  // Verinin geldiği zaman
    }
}
