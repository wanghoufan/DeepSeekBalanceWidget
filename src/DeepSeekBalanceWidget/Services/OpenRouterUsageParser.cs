using System;
using System.Globalization;
using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>Parses the account credits response returned by OpenRouter.</summary>
public static class OpenRouterUsageParser
{
    public static OpenRouterUsageSnapshot? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object)
                return null;

            if (!TryReadDecimal(data, "total_credits", out var totalCredits)
                || !TryReadDecimal(data, "total_usage", out var totalUsage))
                return null;

            return new OpenRouterUsageSnapshot(
                true,
                null,
                Math.Max(0m, totalCredits),
                Math.Max(0m, totalUsage));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadDecimal(JsonElement parent, string name, out decimal value)
    {
        value = 0m;
        if (!parent.TryGetProperty(name, out var element)) return false;

        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDecimal(out value);

        return element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out value);
    }
}
