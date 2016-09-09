namespace Nop.Plugin.Payments.WePay
{
    /// <summary>
    /// Represents WePay payment processor transaction mode
    /// </summary>
    public enum TransactMode : int
    {
        /// <summary>
        /// Create
        /// </summary>
        Create = 1,
        /// <summary>
        /// Create and capture
        /// </summary>
        CreateAndCapture = 2
    }
}
