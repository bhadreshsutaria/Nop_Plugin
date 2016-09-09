using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.WePay.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.WePay.Controllers
{
    public class PaymentWePayController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly PaymentSettings _paymentSettings;
        private readonly WePayPaymentSettings _wePayPaymentSettings;

        public PaymentWePayController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            IPaymentService paymentService, 
            IOrderService orderService, 
            IOrderProcessingService orderProcessingService, 
            IStoreContext storeContext,
            ILogger logger, 
            IWebHelper webHelper,
            PaymentSettings paymentSettings,
            WePayPaymentSettings wePayPaymentSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._paymentSettings = paymentSettings;
            this._wePayPaymentSettings = wePayPaymentSettings;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var wePayPaymentSettings = _settingService.LoadSetting<WePayPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.UseSandbox = wePayPaymentSettings.UseSandbox;
            model.TransactModeId = Convert.ToInt32(wePayPaymentSettings.TransactMode);
            model.AccessToken = wePayPaymentSettings.AccessToken;
            model.AccountId = wePayPaymentSettings.AccountId;
            model.ClientSecret = wePayPaymentSettings.ClientSecret;
            model.AdditionalFee = wePayPaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = wePayPaymentSettings.AdditionalFeePercentage;
            model.ClientId = wePayPaymentSettings.ClientId;
            model.PdtValidateOrderTotal = wePayPaymentSettings.PdtValidateOrderTotal;
            model.TransactModeValues = wePayPaymentSettings.TransactMode.ToSelectList();
            model.FeePayer = wePayPaymentSettings.FeePayer;
            model.RequireShipping = wePayPaymentSettings.RequireShipping;
            model.FundingSources = wePayPaymentSettings.FundingSources;            
            model.CheckoutType = wePayPaymentSettings.CheckoutType;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.UseSandbox, storeScope);
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.TransactMode, storeScope);
                model.AccessToken_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.AccessToken, storeScope);
                model.AccountId_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.AccountId, storeScope);
                model.ClientSecret_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.ClientSecret, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.ClientId_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.ClientId, storeScope);
                model.PdtValidateOrderTotal_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.PdtValidateOrderTotal, storeScope);
                model.CheckoutType_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.CheckoutType, storeScope);
                model.FeePayer_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.FeePayer, storeScope);
                model.RequireShipping_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.RequireShipping, storeScope);
                model.FundingSources_OverrideForStore = _settingService.SettingExists(wePayPaymentSettings, x => x.FundingSources, storeScope);
            }

            return View("~/Plugins/Payments.WePay/Views/PaymentWePay/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var wePayPaymentSettings = _settingService.LoadSetting<WePayPaymentSettings>(storeScope);

            //save settings
            wePayPaymentSettings.UseSandbox = model.UseSandbox;
            wePayPaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
            wePayPaymentSettings.AccessToken = model.AccessToken;
            wePayPaymentSettings.AccountId = model.AccountId;
            wePayPaymentSettings.ClientSecret = model.ClientSecret;
            wePayPaymentSettings.AdditionalFee = model.AdditionalFee;
            wePayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            wePayPaymentSettings.ClientId = model.ClientId;
            wePayPaymentSettings.PdtValidateOrderTotal = model.PdtValidateOrderTotal;
            wePayPaymentSettings.CheckoutType = model.CheckoutType;
            wePayPaymentSettings.FundingSources = model.FundingSources;
            wePayPaymentSettings.RequireShipping = model.RequireShipping;
            wePayPaymentSettings.FeePayer = model.FeePayer;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandbox_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.TransactModeId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.TransactMode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.TransactMode, storeScope);

            if (model.AccessToken_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.AccessToken, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.AccessToken, storeScope);

            if (model.AccountId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.AccountId, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.AccountId, storeScope);

            if (model.ClientSecret_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.ClientSecret, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.ClientSecret, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            if (model.ClientId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.ClientId, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.ClientId, storeScope);
            
            if (model.PdtValidateOrderTotal_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.PdtValidateOrderTotal, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.PdtValidateOrderTotal, storeScope);

            if (model.FeePayer_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.FeePayer, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.FeePayer, storeScope);

            if (model.RequireShipping_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.RequireShipping, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.RequireShipping, storeScope);

            if (model.FundingSources_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.FundingSources, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.FundingSources, storeScope);
            
            if (model.CheckoutType_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(wePayPaymentSettings, x => x.CheckoutType, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(wePayPaymentSettings, x => x.CheckoutType, storeScope);
            

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.WePay/Views/PaymentWePay/PaymentInfo.cshtml", new PaymentInfoModel());
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            return new List<string>();            
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        [ValidateInput(false)]
        public ActionResult PDTHandler(FormCollection form)
        {
            string checkout_id = _webHelper.QueryString<string>("checkout_id");
            Dictionary<string, string> values;
            string response;

            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.WePay") as WePayPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("WePay module cannot be loaded");

            if (processor.GetPDTDetails(checkout_id, out values, out response))
            {
                string orderNumber = string.Empty;
                values.TryGetValue("reference_id", out orderNumber);
                Guid orderNumberGuid = Guid.Empty;
                try
                {
                    orderNumberGuid = new Guid(orderNumber);
                }
                catch(Exception exc) 
                {
                    _logger.Error("Invalid WePay Order Number Guid", exc);
                    throw new NopException("WePay PDT: Invalid order reference number");
                }

                Order order = _orderService.GetOrderByGuid(orderNumberGuid);
                if (order != null)
                {
                    decimal total = decimal.Zero;
                    try
                    {
                        total = decimal.Parse(values["gross"], new CultureInfo("en-US"));
                    }
                    catch (Exception exc)
                    {
                        _logger.Error("WePay PDT. Error getting mc_gross", exc);
                        throw new NopException("WePay PDT: Unable to capture payment");
                    }

                    string payment_status = string.Empty;
                    values.TryGetValue("state", out payment_status);
                    string mc_currency = string.Empty;
                    values.TryGetValue("currency", out mc_currency);
                    string txn_id = string.Empty;
                    values.TryGetValue("checkout_id", out txn_id);
                    string fee = string.Empty;
                    values.TryGetValue("fee", out fee);
                    string shipping_fee = string.Empty;
                    values.TryGetValue("shipping_fee", out shipping_fee);

                    var sb = new StringBuilder();
                    sb.AppendLine("WePay PDT:");
                    sb.AppendLine("total: " + total);
                    sb.AppendLine(" payment status: " + payment_status);
                    sb.AppendLine("currency: " + mc_currency);
                    sb.AppendLine("checkout_id: " + txn_id);
                    sb.AppendLine("fee: " + fee);
                    //sb.AppendLine(" shipping_fee: " + shipping_fee);

                    //order note
                    order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(sb.ToString()));
                    _orderService.UpdateOrder(order);

                    //load settings for a chosen store scope
                    var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                    var wePayPaymentSettings = _settingService.LoadSetting<WePayPaymentSettings>(storeScope);

                    if (wePayPaymentSettings.UseSandbox)
                        _logger.Information(sb.ToString());                    


                    decimal payerTotal = Math.Round(order.OrderTotal + Convert.ToDecimal(fee) + Convert.ToDecimal(shipping_fee), 2);
                    decimal payeeTotal = Math.Round(order.OrderTotal + Convert.ToDecimal(shipping_fee), 2);

                    bool validTotal = true;

                    if (wePayPaymentSettings.PdtValidateOrderTotal)
                    {
                        //validate order total : payer
                        if (wePayPaymentSettings.FeePayer == "payer" && !Math.Round(total, 2).Equals(payerTotal))
                    {
                            string errorStr = string.Format("PDT Validation: WePay order total {0} doesn't equal store order total {1}. Order #{2}", total, payerTotal, order.Id);
                        _logger.Error(errorStr);
                            order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(errorStr));
                            _orderService.UpdateOrder(order);

                            validTotal = false;
                    }
                        //validate order total : payee
                        else if (wePayPaymentSettings.FeePayer == "payee" && !Math.Round(total, 2).Equals(payeeTotal))
                        {
                            string errorStr = string.Format("PDT Validation: WePay order total {0} doesn't equal store order total {1}. Order #{2}", total, payeeTotal, order.Id);
                            _logger.Error(errorStr);
                            order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(errorStr));
                            _orderService.UpdateOrder(order);

                            validTotal = false;
                        }
                    }

                    if (_wePayPaymentSettings.TransactMode == TransactMode.CreateAndCapture && validTotal)
                    {
                        //mark order as paid
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            order.AuthorizationTransactionId = txn_id;
                            _orderService.UpdateOrder(order);
                            _orderProcessingService.MarkOrderAsPaid(order);
                        }
                    }
                    else {
                        //mark order as authorized
                        if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                        {
                            order.AuthorizationTransactionId = txn_id;
                            _orderService.UpdateOrder(order);
                            _orderProcessingService.MarkAsAuthorized(order);
                        }
                    }
                }

                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            else
            {
                string orderNumber = string.Empty;
                values.TryGetValue("custom", out orderNumber);
                Guid orderNumberGuid = Guid.Empty;
                try
                {
                    orderNumberGuid = new Guid(orderNumber);
                }
                catch (Exception exc)
                {
                    _logger.Error("Invalid WePay Order Number Guid", exc);
                    throw new NopException("WePay PDT: Invalid order reference number");
                }

                Order order = _orderService.GetOrderByGuid(orderNumberGuid);
                if (order != null)
                {
                    //order note
                    string note = "WePay redirect PDT failed. " + response;
                    order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(note));
                    _orderService.UpdateOrder(order);
                    _logger.Error(note);
                }

                return RedirectToAction("Orders", "Customer", new { area = "" });
            }
        }

        [ValidateInput(false)]
        public void WCBHandler(FormCollection form)
        {
            try
            {
                string checkout_id = form["checkout_id"];
                Dictionary<string, string> values;
                string response;

                var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.WePay") as WePayPaymentProcessor;
                if (processor == null ||
                    !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                    throw new NopException("WePay module cannot be loaded");

                if (processor.GetPDTDetails(checkout_id, out values, out response))
                {
                    string orderNumber = string.Empty;
                    values.TryGetValue("reference_id", out orderNumber);
                    Guid orderNumberGuid = Guid.Empty;
                    try
                    {
                        orderNumberGuid = new Guid(orderNumber);
                    }
                    catch (Exception exc)
                    {
                        _logger.Error("Invalid WePay Order Number Guid", exc);
                        throw new NopException("WePay PDT: Invalid order reference number");
                    }
                    Order order = _orderService.GetOrderByGuid(orderNumberGuid);
                    if (order != null)
                    {
                        decimal total = decimal.Zero;
                        try
                        {
                            total = decimal.Parse(values["gross"], new CultureInfo("en-US"));
                        }
                        catch (Exception exc)
                        {
                            _logger.Error("WePay callback PDT. Error getting mc_gross", exc);
                            throw new NopException("WePay PDT: Invalid order reference number");
                        }

                        string payment_status = string.Empty;
                        values.TryGetValue("state", out payment_status);
                        string mc_currency = string.Empty;
                        values.TryGetValue("currency", out mc_currency);
                        string txn_id = string.Empty;
                        values.TryGetValue("checkout_id", out txn_id);
                        string fee = string.Empty;
                        values.TryGetValue("fee", out fee);
                        string shipping_fee = string.Empty;
                        values.TryGetValue("shipping_fee", out shipping_fee);

                        var sb = new StringBuilder();
                        sb.AppendLine("WePay PDT:");
                        sb.AppendLine("total: " + total);
                        sb.AppendLine("Payment status: " + payment_status);
                        sb.AppendLine("currency: " + mc_currency);
                        sb.AppendLine("checkout_id: " + txn_id);
                        sb.AppendLine("fee: " + fee);
                        //sb.AppendLine("shipping_fee: " + shipping_fee);

                        //order note
                        order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(sb.ToString()));
                        _orderService.UpdateOrder(order);

                        //load settings for a chosen store scope
                        var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                        var wePayPaymentSettings = _settingService.LoadSetting<WePayPaymentSettings>(storeScope);

                        if (wePayPaymentSettings.UseSandbox)
                            _logger.Information(sb.ToString());                    

                        decimal payerTotal = Math.Round(order.OrderTotal + Convert.ToDecimal(fee) + Convert.ToDecimal(shipping_fee), 2);
                        decimal payeeTotal = Math.Round(order.OrderTotal + Convert.ToDecimal(shipping_fee), 2);

                        bool validTotal = true;

                        if (wePayPaymentSettings.PdtValidateOrderTotal)
                        {
                            //validate order total : payer
                            if (wePayPaymentSettings.FeePayer == "payer" && !Math.Round(total, 2).Equals(payerTotal))
                            {
                                string errorStr = string.Format("PDT Validation: WePay order total {0} doesn't equal order total {1}. Order #{2}", total, payerTotal, order.Id);
                            _logger.Error(errorStr);
                                order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(errorStr));
                                _orderService.UpdateOrder(order);

                                validTotal = false;
                        }
                            //validate order total : payee
                            else if (wePayPaymentSettings.FeePayer == "payee" && !Math.Round(total, 2).Equals(payeeTotal))
                            {
                                string errorStr = string.Format("PDT Validation: WePay order total {0} doesn't equal order total {1}. Order #{2}", total, payeeTotal, order.Id);
                                _logger.Error(errorStr);
                                order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(errorStr));
                                _orderService.UpdateOrder(order);

                                validTotal = false;
                            }
                        }

                        if (_wePayPaymentSettings.TransactMode == TransactMode.CreateAndCapture && validTotal)
                        {
                            //mark order as paid
                            if (_orderProcessingService.CanMarkOrderAsPaid(order))
                            {
                                order.AuthorizationTransactionId = txn_id;
                                _orderService.UpdateOrder(order);
                                _orderProcessingService.MarkOrderAsPaid(order);
                            }
                        }
                        else
                        {
                            //mark order as authorized
                            if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                            {
                                order.AuthorizationTransactionId = txn_id;
                                _orderService.UpdateOrder(order);
                                _orderProcessingService.MarkAsAuthorized(order);
                            }
                        }
                    }
                }
                else
                {
                    string orderNumber = string.Empty;
                    values.TryGetValue("custom", out orderNumber);
                    Guid orderNumberGuid = Guid.Empty;
                    try
                    {
                        orderNumberGuid = new Guid(orderNumber);
                    }
                    catch (Exception exc)
                    {
                        _logger.Error("Invalid WePay Order Number Guid", exc);
                        throw new NopException("WePay PDT: Invalid order reference number");
                    }

                    Order order = _orderService.GetOrderByGuid(orderNumberGuid);
                    if (order != null)
                    {
                        //order note
                        string note = "WePay callback PDT failed. " + response;
                        order.OrderNotes.Add(WePayPaymentHelper.GenerateOrderNote(note));
                        _orderService.UpdateOrder(order);
                        _logger.Error(note);

                    }
                }
            }
            catch (Exception exc)
            {
                _logger.Error(exc.Message, exc);
            }
        }
    }
}