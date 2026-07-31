using System;
using System.Collections.Generic;
using System.Linq;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed record CurrencySelection(ParsedBalance? Balance, string SelectedCurrency, bool Found);

public static class CurrencySelector
{
    public static CurrencySelection Select(IReadOnlyList<ParsedBalance> balances, string selectedCurrency)
    {
        var found = balances.FirstOrDefault(b =>
            string.Equals(b.Currency, selectedCurrency, StringComparison.OrdinalIgnoreCase));
        return found is not null
            ? new CurrencySelection(found, selectedCurrency, true)
            : new CurrencySelection(null, selectedCurrency, false);
    }
}
