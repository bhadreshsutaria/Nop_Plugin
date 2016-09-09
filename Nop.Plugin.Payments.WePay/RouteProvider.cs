using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.WePay
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //PDT
            routes.MapRoute("Plugin.Payments.WePay.PDTHandler",
                 "Plugins/PaymentWePay/PDTHandler",
                 new { controller = "PaymentWePay", action = "PDTHandler" },
                 new[] { "Nop.Plugin.Payments.WePay.Controllers" }
            );

            //WCB
            routes.MapRoute("Plugin.Payments.WePay.WCBHandler",
                 "Plugins/PaymentWePay/WCBHandler",
                 new { controller = "PaymentWePay", action = "WCBHandler" },
                 new[] { "Nop.Plugin.Payments.WePay.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
