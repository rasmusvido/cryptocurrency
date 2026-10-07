public class Converter {
    // Pris pr. enhed i dollars for hver kendt kryptovaluta, slået op på navn.
    private readonly Dictionary<string, double> pricesInDollars = new Dictionary<string, double>();

    //Main method
    public static void Main(string[] args) {}

    /// <summary>
    /// Angiver prisen for en enhed af en kryptovaluta. Prisen angives i dollars.
    /// Hvis der tidligere er angivet en værdi for samme kryptovaluta,
    /// bliver den gamle værdi overskrevet af den nye værdi
    /// </summary>
    /// <param name="currencyName">Navnet på den kryptovaluta der angives</param>
    /// <param name="price">Prisen på en enhed af valutaen målt i dollars. Prisen kan ikke være negativ</param>
    public void SetPricePerUnit(String currencyName, double price) {
        ValidateCurrencyName(currencyName, nameof(currencyName));

        if (double.IsNaN(price) || double.IsInfinity(price)) {
            throw new ArgumentException("Prisen skal være et endeligt tal.", nameof(price));
        }
        if (price < 0) {
            throw new ArgumentException("Prisen kan ikke være negativ.", nameof(price));
        }

        // Indexeren tilføjer valutaen, eller overskriver den gamle pris hvis den findes i forvejen.
        pricesInDollars[currencyName] = price;
    }

    /// <summary>
    /// Konverterer fra en kryptovaluta til en anden.
    /// Hvis en af de angivne valutaer ikke findes, kaster funktionen en ArgumentException
    ///
    /// </summary>
    /// <param name="fromCurrencyName">Navnet på den valuta, der konverterers fra</param>
    /// <param name="toCurrencyName">Navnet på den valuta, der konverteres til</param>
    /// <param name="amount">Beløbet angivet i valutaen angivet i fromCurrencyName</param>
    /// <returns>Værdien af beløbet i toCurrencyName</returns>
    public double Convert(String fromCurrencyName, String toCurrencyName, double amount) {
        double fromPrice = GetPrice(fromCurrencyName, nameof(fromCurrencyName));
        double toPrice = GetPrice(toCurrencyName, nameof(toCurrencyName));

        if (double.IsNaN(amount) || double.IsInfinity(amount)) {
            throw new ArgumentException("Beløbet skal være et endeligt tal.", nameof(amount));
        }
        if (amount < 0) {
            throw new ArgumentException("Beløbet kan ikke være negativt.", nameof(amount));
        }

        // Samme valuta: intet at omregne (undgår også afrundingsfejl og division med 0).
        if (fromCurrencyName == toCurrencyName) {
            return amount;
        }

        if (toPrice == 0) {
            throw new ArgumentException(
                $"Kan ikke omregne til '{toCurrencyName}', fordi dens pris er 0 dollars.", nameof(toCurrencyName));
        }

        // Omregn først til dollars, derefter fra dollars til målvalutaen.
        double result = amount * fromPrice / toPrice;

        if (double.IsInfinity(result)) {
            throw new OverflowException("Resultatet af omregningen er for stort til at blive repræsenteret.");
        }

        return result;
    }

    /// <summary>
    /// Slår prisen op for en valuta. Kaster ArgumentException hvis navnet er ugyldigt
    /// eller valutaen ikke findes.
    /// </summary>
    private double GetPrice(string currencyName, string parameterName) {
        ValidateCurrencyName(currencyName, parameterName);

        if (!pricesInDollars.TryGetValue(currencyName, out double price)) {
            throw new ArgumentException($"Valutaen '{currencyName}' findes ikke.", parameterName);
        }
        return price;
    }

    private static void ValidateCurrencyName(string currencyName, string parameterName) {
        if (string.IsNullOrWhiteSpace(currencyName)) {
            throw new ArgumentException("Valutanavnet må ikke være tomt.", parameterName);
        }
    }
}
