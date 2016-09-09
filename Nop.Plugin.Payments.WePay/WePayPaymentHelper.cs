using System;
using System.Net;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.WePay
{
    public class WePayPaymentHelper
    {

        public static WebClient WePayWebClient(WePayPaymentSettings _wepayPaymentSettings)
        {
            WebClient client = new WebClient();
            client.Headers.Add("Authorization", "Bearer " + _wepayPaymentSettings.AccessToken);
            client.Headers.Add("Content-Type", "application/json");
            client.Headers.Add("User-Agent", "NopCommerce WePay Plugin");

            return client;
        }

        public static OrderNote GenerateOrderNote(string note)
        {
            return GenerateOrderNote(note, false);
        }

        public static OrderNote GenerateOrderNote(string note, bool displayToCustomer)
        {
            return new OrderNote()
            {
                Note = note,
                DisplayToCustomer = displayToCustomer,
                CreatedOnUtc = DateTime.UtcNow
            };
        }
    }
}
