using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using System.Web.Mvc;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.WePay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        public int TransactModeId { get; set; }
        public bool TransactModeId_OverrideForStore { get; set; }
        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.TransactModeValues")]
        public SelectList TransactModeValues { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.AccessToken")]
        public string AccessToken { get; set; }
        public bool AccessToken_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.AccountId")]
        public long AccountId { get; set; }
        public bool AccountId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.ClientSecret")]
        public string ClientSecret { get; set; }
        public bool ClientSecret_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.ClientId")]
        public string ClientId { get; set; }
        public bool ClientId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.PDTValidateOrderTotal")]
        public bool PdtValidateOrderTotal { get; set; }
        public bool PdtValidateOrderTotal_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.AppFee")]
        public decimal AppFee { get; set; }
        public bool AppFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.FeePayer")]
        public string FeePayer { get; set; }
        public bool FeePayer_OverrideForStore { get; set; }
        public SelectList FeePayers { 
            get 
            {
                var options = new List<SelectListItem> { 
                    new SelectListItem { Value= "payer", Text = "[Payer] Customer will pay amount + fees, and you will receive amount" }, 
                    new SelectListItem { Value= "payee", Text = "[Payee] Customer will pay amount, and you will receive amount - fees" },                                       
                };
                return new SelectList(options, "Value", "Text");
            } 
        }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.RequireShipping")]
        public bool RequireShipping { get; set; }
        public bool RequireShipping_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.FundingSources")]
        public string FundingSources { get; set; }
        public bool FundingSources_OverrideForStore { get; set; }
        public SelectList FundingSourcesValues
        {
            get
            {
                var options = new List<SelectListItem> { 
                    new SelectListItem { Value= "bank,cc", Text = "Accepts Bank Account and Credit Card" }, 
                    new SelectListItem { Value= "bank", Text = "Accepts Bank Account Only" }, 
                    new SelectListItem { Value= "cc", Text = "Accepts Credit Card Only" },                                       
                };
                return new SelectList(options, "Value", "Text");
            } 
        }

        [NopResourceDisplayName("Plugins.Payments.WePay.Fields.CheckoutType")]
        public string CheckoutType { get; set; }
        public bool CheckoutType_OverrideForStore { get; set; }
        public SelectList CheckoutTypes
        {
            get
            {
                var options = new List<SelectListItem> { 
                    new SelectListItem { Value= "GOODS", Text = "Goods" }, 
                    new SelectListItem { Value= "SERVICE", Text = "Service" },
                    new SelectListItem { Value= "DONATION", Text = "Donation" },
                    new SelectListItem { Value= "EVENT", Text = "Event" },
                    new SelectListItem { Value= "PERSONAL", Text = "Personal" },
                };
                return new SelectList(options, "Value", "Text");
            } 
        }
    }
}