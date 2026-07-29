using ReadyToGoTravel.Search.Contracts;

namespace ReadyToGoTravel.Search.Pricing;

public enum CurrencyProvenance
{
    SupplierReturned,
    RequestedPointOfSale,
}

public sealed record OfferPrice(
    decimal MinimumTotal,
    string ReturnedCurrency,
    string RequestedCurrency,
    CurrencyProvenance CurrencyProvenance,
    decimal BaseAmount,
    decimal IncludedTaxes,
    decimal IncludedFees)
{
    public static SearchResult<OfferPrice> Create(
        decimal minimumTotal,
        string returnedCurrency,
        string requestedCurrency,
        CurrencyProvenance currencyProvenance,
        decimal baseAmount,
        decimal includedTaxes,
        decimal includedFees)
    {
        if (minimumTotal <= 0m || baseAmount < 0m || includedTaxes < 0m || includedFees < 0m)
        {
            return SearchResult.Failure<OfferPrice>("invalid_price_amount");
        }

        if (!SearchCodeValidation.IsCurrency(returnedCurrency)
            || !SearchCodeValidation.IsCurrency(requestedCurrency))
        {
            return SearchResult.Failure<OfferPrice>("invalid_currency");
        }

        if (!Enum.IsDefined(currencyProvenance))
        {
            return SearchResult.Failure<OfferPrice>("invalid_currency_provenance");
        }

        if (baseAmount + includedTaxes + includedFees > minimumTotal)
        {
            return SearchResult.Failure<OfferPrice>("price_components_exceed_total");
        }

        return SearchResult.Success(new OfferPrice(
            minimumTotal,
            returnedCurrency,
            requestedCurrency,
            currencyProvenance,
            baseAmount,
            includedTaxes,
            includedFees));
    }
}
