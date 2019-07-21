namespace EmailRelay.Logic.Models
{
    public enum RelayAuthResult
    {
        Unknown = 0,
        Authorized,
        InvalidSender,
        DkimFail,
        SpfFail
    }
}
