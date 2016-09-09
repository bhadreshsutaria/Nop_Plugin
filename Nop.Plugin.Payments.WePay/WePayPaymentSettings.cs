using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.WePay
{
    public class WePayPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public TransactMode TransactMode { get; set; }
        public string AccessToken { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
        public long AccountId { get; set; }
        public string ClientSecret { get; set; }
        public string ClientId { get; set; }
        public string checkout_uri { get; set; }
        public bool PdtValidateOrderTotal { get; set; }

        public string CheckoutType { get; set; }
        public string FeePayer { get; set; }
        public bool RequireShipping { get; set; }
        public decimal ShippingFee { get; set; }
        public string FundingSources { get; set; }
        public string Currency { get; set; }
    }
}
