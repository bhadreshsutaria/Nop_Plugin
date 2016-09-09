using System;
using System.Web;
using Nop.Core.Plugins;
using Nop.Services.Payments;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core;
using Nop.Services.Orders;
using Nop.Services.Tax;
using Nop.Services.Localization;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using Nop.Plugin.Payments.WePay.Controllers;
using System.Web.Routing;
using Nop.Core.Domain.Payments;
using Newtonsoft.Json;
using Nop.Services.Stores;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.WePay
{
    /// <summary>
    /// PayPalStandard payment processor
    /// </summary>
    public class WePayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields
        private readonly IStoreService _storeService;
        private readonly WePayPaymentSettings _wepayPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ITaxService _taxService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly HttpContextBase _httpContext;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        #endregion

        #region Ctor

        public WePayPaymentProcessor(WePayPaymentSettings wepayStandardPaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ICheckoutAttributeParser checkoutAttributeParser, ITaxService taxService, 
            IOrderTotalCalculationService orderTotalCalculationService, HttpContextBase httpContext,
            IStoreService storeService, ILogger logger, IStoreContext storeContext, ICustomerService customerService)
        {
            this._wepayPaymentSettings = wepayStandardPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._taxService = taxService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._httpContext = httpContext;
            this._storeService = storeService;
            this._logger = logger;
            this._storeContext = storeContext;
            this._customerService = customerService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets Paypal URL
        /// </summary>
        /// <returns></returns>
        private string GetWePayUrl()
        {
            return _wepayPaymentSettings.UseSandbox ? "https://stage.wepayapi.com/v2/" :
                "https://wepayapi.com/v2/";
        }
        /// <summary>
        /// Gets PDT details
        /// </summary>
        /// <param name="tx">TX</param>
        /// <param name="values">Values</param>
        /// <param name="response">Response</param>
        /// <returns>Result</returns>
        public bool GetPDTDetails(string checkout_id, out Dictionary<string, string> values, out string response)
        {
            WebClient client = WePayPaymentHelper.WePayWebClient(_wepayPaymentSettings);

            long iid = 0;
            bool valid = Int64.TryParse(checkout_id, out iid);

            var outresp = new CheckoutRequest { checkout_id = iid };
            bool success = false;
            string uriString = GetWePayUrl() + outresp.actionUrl;
            var data = JsonConvert.SerializeObject(outresp);
            var json = "";
            response = null;
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                
                json = (data.Length > 3) ? 
                    client.UploadString(new Uri(uriString), "POST", data) : 
                    client.DownloadString(new Uri(uriString));
                
                dynamic jsonresponse = JsonConvert.DeserializeObject(json);
                values.Add("reference_id", Convert.ToString(jsonresponse.reference_id));
                values.Add("checkout_id", Convert.ToString(jsonresponse.checkout_id));
                values.Add("gross", Convert.ToString(jsonresponse.gross));
                values.Add("state", Convert.ToString(jsonresponse.state));
                values.Add("currency", Convert.ToString(jsonresponse.currency));
                values.Add("fee", Convert.ToString(jsonresponse.fee));
                values.Add("shipping_fee", Convert.ToString(jsonresponse.shipping_fee));

                success = true;

                // Log details for testing
                if (_wepayPaymentSettings.UseSandbox)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("PDT Details:");
                    sb.AppendLine(" checkout_id: " + checkout_id);
                    sb.AppendLine(" uriString: " + uriString);
                    sb.AppendLine(" response: " + json);
                    _logger.Information(sb.ToString());
            }
            }
            catch (WebException we)
            {
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse httpErrorResponse = (HttpWebResponse)we.Response as HttpWebResponse;

                    StreamReader reader = new StreamReader(httpErrorResponse.GetResponseStream(), Encoding.UTF8);
                    string responseBody = reader.ReadToEnd();
                    var errResp = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                    response = string.Format("Error: {0}" + Environment.NewLine + " Error Message: {1}" + Environment.NewLine + " Error Description: {2}", errResp.error, errResp.error_description, we.Message);
                }
                else
                    response = string.Format("Error: {0}", we.Message);
            }
            catch (Exception exc) {
                response = string.Format("Error: {0}", exc.Message);
            }

            if (!string.IsNullOrEmpty(response))
                _logger.Error(response);

            return success;
        }

        WebClientResponse ProcessWebClient(dynamic outrequest)
        {
            WebClientResponse wcresponse = new WebClientResponse();
            List<string> errors = new List<string>();
            WebClient client = WePayPaymentHelper.WePayWebClient(_wepayPaymentSettings);            
            string uriString = GetWePayUrl() + outrequest.actionUrl;
            var data = JsonConvert.SerializeObject(outrequest);
            try
            {
                client.UploadString(new Uri(uriString), "POST", data);

                var chkoutreq = new CheckoutRequest { checkout_id = Convert.ToInt64(outrequest.checkout_id) };
                data = JsonConvert.SerializeObject(chkoutreq);
                uriString = GetWePayUrl() + chkoutreq.actionUrl;
                client = WePayPaymentHelper.WePayWebClient(_wepayPaymentSettings);                   
                var clientresp = client.UploadString(new Uri(uriString), "POST", data);
                dynamic response = JsonConvert.DeserializeObject(clientresp);
                wcresponse.state = Convert.ToString(response.state);
            }
            catch (WebException we)
            {
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse httpErrorResponse = (HttpWebResponse)we.Response as HttpWebResponse;
                    StreamReader reader = new StreamReader(httpErrorResponse.GetResponseStream(), Encoding.UTF8);
                    string responseBody = reader.ReadToEnd();
                    var errResp = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                    errors.Add(string.Format("Error: {0};" + Environment.NewLine + " Error Message: {1};" + Environment.NewLine + " Error Description: {2}", errResp.error, errResp.error_description, we.Message));
                }
                else
                    errors.Add(string.Format("Error: {0}", we.Message));
            }
            catch (Exception exc)
            {
                errors.Add(string.Format("Error: {0}", exc.Message));
            }
            wcresponse.Errors = errors;
            return wcresponse;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult() { NewPaymentStatus = PaymentStatus.Pending };
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);

            WebClient client = WePayPaymentHelper.WePayWebClient(_wepayPaymentSettings);
            string returnUrl = _webHelper.GetStoreLocation(_storeContext.CurrentStore.SslEnabled) + "Plugins/PaymentWePay/PDTHandler";
            string callbackUrl = _webHelper.GetStoreLocation(_storeContext.CurrentStore.SslEnabled) + "Plugins/PaymentWePay/WCBHandler";
                        
            var orderTotal = Math.Round(processPaymentRequest.OrderTotal, 2);
            var shipping = 0; 
            
            CheckoutCreateCaptureRequest outrequest = new CheckoutCreateCaptureRequest
            {
                account_id = _wepayPaymentSettings.AccountId,
                type = _wepayPaymentSettings.CheckoutType,
                amount = orderTotal,
                short_description = _storeService.GetStoreById(processPaymentRequest.StoreId).Name,
                long_description = string.Format("{0} order #{1}", _storeService.GetStoreById(processPaymentRequest.StoreId).Name, processPaymentRequest.OrderGuid),
                reference_id = processPaymentRequest.OrderGuid.ToString(),
                auto_capture = (_wepayPaymentSettings.TransactMode == TransactMode.CreateAndCapture),
                redirect_uri = returnUrl,
                callback_uri = callbackUrl,
                fee_payer = _wepayPaymentSettings.FeePayer,
                require_shipping = _wepayPaymentSettings.RequireShipping,
                currency = _wepayPaymentSettings.Currency,
                funding_sources = _wepayPaymentSettings.FundingSources,
                shipping_fee = shipping,
                prefill_info = new PrefillInfo() { name = customer.BillingAddress.FirstName + " " + customer.BillingAddress.LastName, email = customer.BillingAddress.Email }
            };
            string uriString = GetWePayUrl() + outrequest.actionUrl;
            var data = JsonConvert.SerializeObject(outrequest);
            try
            {
                var clientresp = client.UploadString(new Uri(uriString), "POST", data);

                dynamic response = JsonConvert.DeserializeObject(clientresp);
                var settings = new WePayPaymentSettings()
                {
                    checkout_uri = response.checkout_uri,
                    UseSandbox = _wepayPaymentSettings.UseSandbox,
                    TransactMode = _wepayPaymentSettings.TransactMode,
                    AccessToken = _wepayPaymentSettings.AccessToken,
                    AccountId = _wepayPaymentSettings.AccountId,
                    ClientSecret = _wepayPaymentSettings.ClientSecret,
                    AdditionalFee = _wepayPaymentSettings.AdditionalFee,
                    AdditionalFeePercentage = _wepayPaymentSettings.AdditionalFeePercentage,
                    ClientId = _wepayPaymentSettings.ClientId,
                    PdtValidateOrderTotal = _wepayPaymentSettings.PdtValidateOrderTotal,
                    FeePayer = _wepayPaymentSettings.FeePayer,
                    RequireShipping = _wepayPaymentSettings.RequireShipping,
                    Currency = _wepayPaymentSettings.Currency,
                    FundingSources = _wepayPaymentSettings.FundingSources,
                    CheckoutType = _wepayPaymentSettings.CheckoutType
                };
                _settingService.SaveSetting(settings);

            }
            catch (WebException we)
            {
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse httpErrorResponse = (HttpWebResponse)we.Response as HttpWebResponse;

                    StreamReader reader = new StreamReader(httpErrorResponse.GetResponseStream(), Encoding.UTF8);
                    string responseBody = reader.ReadToEnd();
                    var errResp = JsonConvert.DeserializeObject<ErrorResponse>(responseBody);
                    result.AddError(string.Format("Error: {0};" + Environment.NewLine + " Error Message: {1};" + Environment.NewLine + " Error Description: {2}", errResp.error, errResp.error_description, we.Message));
                }
                else
                {
                    result.AddError(string.Format("Error: {0}", we.Message));
                }
            }
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            _httpContext.Response.Redirect(_wepayPaymentSettings.checkout_uri);
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }        

        public decimal GetAdditionalHandlingFee(System.Collections.Generic.IList<Core.Domain.Orders.ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _wepayPaymentSettings.AdditionalFee, _wepayPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            CheckoutCaptureRequest outrequest = new CheckoutCaptureRequest
            {
                checkout_id = capturePaymentRequest.Order.AuthorizationTransactionId
            };

            WebClientResponse wcresponse = ProcessWebClient(outrequest);
            foreach (string error in wcresponse.Errors)
                result.AddError(error);
            if (result.Errors.Count <= 0 && wcresponse.state == "captured")
                result.NewPaymentStatus = PaymentStatus.Paid;
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            dynamic outrequest;
            if (refundPaymentRequest.IsPartialRefund)
            {
                outrequest = new CheckoutPartialRefundRequest();
                outrequest.amount = refundPaymentRequest.AmountToRefund;
            }
            else
                outrequest = new CheckoutRefundRequest();

            outrequest.checkout_id = refundPaymentRequest.Order.AuthorizationTransactionId;
            outrequest.refund_reason = "Refund for Order #" + refundPaymentRequest.Order.OrderGuid;

            WebClientResponse wcresponse = ProcessWebClient(outrequest);
            foreach (string error in wcresponse.Errors)
                result.AddError(error);
            if (result.Errors.Count <= 0 && wcresponse.state == "refunded")
            {
                if (refundPaymentRequest.IsPartialRefund)
                    result.NewPaymentStatus = PaymentStatus.PartiallyRefunded;
                else
                    result.NewPaymentStatus = PaymentStatus.Refunded;
            }

            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            CheckoutCancelRequest outrequest = new CheckoutCancelRequest
            {
                checkout_id = voidPaymentRequest.Order.AuthorizationTransactionId,
                cancel_reason = "Cancel payment for Order #" + voidPaymentRequest.Order.OrderGuid
            };
            WebClientResponse wcresponse = ProcessWebClient(outrequest);
            foreach (string error in wcresponse.Errors)
                result.AddError(error);
            if (result.Errors.Count <= 0 && wcresponse.state == "cancelled")
                result.NewPaymentStatus = PaymentStatus.Voided;
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Core.Domain.Orders.Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentWePay";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.WePay.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out System.Web.Routing.RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentWePay";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.WePay.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentWePayController);
        }

        public override void Install()
        {
            //settings
            var settings = new WePayPaymentSettings()
            {
                UseSandbox = true,
                TransactMode = TransactMode.Create,
                PdtValidateOrderTotal = true,
                CheckoutType = "SERVICE",
                Currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode                
            };
            _settingService.SaveSetting(settings);

            //locales

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.TransactModeValues", "Transaction Mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.TransactModeValues.Hint", "Choose transaction mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AccessToken", "Access Token");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AccessToken.Hint", "Specify your WePay Access Token.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AccountId", "Account ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AccountId.Hint", "Specify your WePay Account ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientSecret", "Client Secret");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientSecret.Hint", "Specify your WePay ClientSecret.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientId", "Client ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientId.Hint", "Specify your WePay Client ID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.PDTValidateOrderTotal", "Validate Order Total");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.PDTValidateOrderTotal.Hint", "Check if PDT (payment data transfer) handler should validate order totals.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFee", "Additional Fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFeePercentage", "Additional Fee Use Percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirectUrl", "Redirect Url (After Payment)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirectUrl.Hint", "Leave blank to use the default Redirect Url.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirecionTip", "You will be redirected to WePay site to complete the order.");

            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.FeePayer", "WePay Fee Payer");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.FeePayer.Hint", "Who will pay the WePay transaction fee?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.RequireShipping", "Require Shipping Address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.RequireShipping.Hint", "If set to true then the payer will be asked to enter a shipping address when they pay.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.FundingSources", "Funding Sources");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.FundingSources.Hint", "What funding sources you want to accept for this checkout.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.CheckoutType", "Checkout Type");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.WePay.Fields.CheckoutType.Hint", "The checkout type (one of the following: GOODS, SERVICE, DONATION, EVENT or PERSONAL)");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<WePayPaymentSettings>();

            //locales
            
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.TransactModeValues");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.TransactModeValues.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AccessToken");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AccessToken.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AccountId");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AccountId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientSecret");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientSecret.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientId");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.ClientId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.PDTValidateOrderTotal");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.PDTValidateOrderTotal.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirectUrl");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirectUrl.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.RedirecionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.FeePayer");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.FeePayer.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.RequireShipping");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.RequireShipping.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.FundingSources");
            this.DeletePluginLocaleResource("Plugins.Payments.WePay.Fields.FundingSources.Hint");

            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
    
    public class CheckoutCreateCaptureRequest
    {
        [JsonIgnore]
        public readonly string actionUrl = @"checkout/create";

        public long account_id { get; set; }
        public string short_description { get; set; }
        public string type { get; set; }
        public decimal amount { get; set; }
        public string reference_id { get; set; }
        public string redirect_uri { get; set; }
        public bool auto_capture { get; set; }
        public string callback_uri { get; set; }

        public string fee_payer { get; set; }
        public decimal app_fee { get; set; }
        public bool require_shipping { get; set; }
        public decimal shipping_fee { get; set; }
        public string funding_sources { get; set; }
        public string long_description { get; set; }
        public string currency { get; set; }
        public PrefillInfo prefill_info { get; set; }
    }

    public class PrefillInfo
    {
        public string name { get; set; }
        public string email { get; set; }
    }

    public class CheckoutRefundRequest
    {
        [JsonIgnore]
        public readonly string actionUrl = @"checkout/refund";
        public string checkout_id { get; set; }
        public string refund_reason { get; set; }
    }
    public class CheckoutPartialRefundRequest
    {
        [JsonIgnore]
        public readonly string actionUrl = @"checkout/refund";
        public string checkout_id { get; set; }
        public string refund_reason { get; set; }
        public decimal amount { get; set; }
    }
    public class CheckoutCancelRequest
    {
        [JsonIgnore]
        public readonly string actionUrl = @"checkout/cancel";
        public string checkout_id { get; set; }
        public string cancel_reason { get; set; }
    }
    public class CheckoutRequest
    {
        public long checkout_id { get; set; }

        [JsonIgnore]
        public readonly string actionUrl = @"checkout";
    }
    public class CheckoutCaptureRequest
    {
        public string checkout_id { get; set; }

        [JsonIgnore]
        public readonly string actionUrl = @"checkout/capture";
    }
    public class ErrorResponse
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
    public class WebClientResponse {
        public string state { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }
}
