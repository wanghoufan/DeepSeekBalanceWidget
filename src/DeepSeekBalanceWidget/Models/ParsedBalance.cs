namespace DeepSeekBalanceWidget.Models;

public sealed record ParsedBalance(
    string Currency,
    decimal Total,
    decimal Granted,
    decimal ToppedUp,
    bool IsAvailable);
