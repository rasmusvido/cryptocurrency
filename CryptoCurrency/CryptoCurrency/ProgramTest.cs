using Xunit;

public class ProgramTest {

    // Antal decimaler der sammenlignes på, da double-beregninger kan give små afrundingsfejl.
    private const int Precision = 10;

    // Hjælpemetode: laver en Converter med to valutaer og deres priser i dollars.
    private static Converter CreateConverter(string fromName, double fromPrice, string toName, double toPrice) {
        var converter = new Converter();
        converter.SetPricePerUnit(fromName, fromPrice);
        converter.SetPricePerUnit(toName, toPrice);
        return converter;
    }

    // =====================================================================
    // Krav fra kommentarerne i Converter
    // =====================================================================

    // --- Convert: korrekt omregning mellem to forskellige valutaer ---
    [Theory]
    [InlineData(60000, 3000, 1, 20)]            // Dyr til billig valuta: 1 BTC (60000$) = 20 ETH (3000$)
    [InlineData(3000, 60000, 1, 0.05)]          // Billig til dyr valuta: 1 ETH = 0,05 BTC
    [InlineData(100, 50, 2.5, 5)]               // Beløb med decimaler
    [InlineData(0.5, 2, 10, 2.5)]               // Priser under 1 dollar
    [InlineData(100, 100, 3, 3)]                // Forskellige valutaer med samme pris: beløbet skal være uændret
    [InlineData(60000, 3000, 0, 0)]             // Grænseværdi: beløb 0 giver 0
    [InlineData(0, 100, 5, 0)]                  // Grænseværdi: fra-valuta med pris 0 er værdiløs
    [InlineData(1, 0.00000095367431640625, 1, 1048576)] // Meget lille pris (2^-20) på til-valutaen giver stort resultat
    [InlineData(1000000, 1, 1000, 1000000000)]  // Store tal
    public void Convert_ReturnererKorrektResultat(
        double fromPrice, double toPrice, double amount, double expected) {
        // Arrange
        var converter = CreateConverter("FROM", fromPrice, "TO", toPrice);

        // Act
        double result = converter.Convert("FROM", "TO", amount);

        // Assert
        Assert.Equal(expected, result, Precision);
    }

    // --- Convert: omregning fra en valuta til sig selv ---
    [Theory]
    [InlineData(60000, 1)]    // Pris forskellig fra 1: fanger fejl hvor beløbet ganges med prisen
    [InlineData(60000, 2.5)]  // Beløb med decimaler
    [InlineData(60000, 0)]    // Grænseværdi: beløb 0
    [InlineData(1, 1000)]     // Pris præcis 1
    public void Convert_SammeValuta_ReturnererSammeBeloeb(double price, double amount) {
        // Arrange
        var converter = new Converter();
        converter.SetPricePerUnit("BTC", price);

        // Act
        double result = converter.Convert("BTC", "BTC", amount);

        // Assert
        Assert.Equal(amount, result, Precision);
    }

    // --- Convert: omregning frem og tilbage giver det oprindelige beløb ---
    [Theory]
    [InlineData(60000, 3000, 1)]
    [InlineData(0.37, 12345.67, 89.1)]
    public void Convert_FremOgTilbage_GiverOprindeligtBeloeb(double fromPrice, double toPrice, double amount) {
        // Arrange
        var converter = CreateConverter("FROM", fromPrice, "TO", toPrice);

        // Act
        double there = converter.Convert("FROM", "TO", amount);
        double back = converter.Convert("TO", "FROM", there);

        // Assert
        Assert.Equal(amount, back, Precision);
    }

    // --- Convert: ukendt valuta skal kaste ArgumentException ---
    [Theory]
    [InlineData("BTC", "XXX")]  // Til-valuta findes ikke
    [InlineData("XXX", "BTC")]  // Fra-valuta findes ikke
    [InlineData("XXX", "YYY")]  // Ingen af valutaerne findes
    [InlineData("btc", "ETH")]  // Navne skelner mellem store og små bogstaver
    [InlineData(null, "BTC")]   // Null som fra-valuta
    [InlineData("BTC", null)]   // Null som til-valuta
    public void Convert_UkendtValuta_KasterArgumentException(string? fromName, string? toName) {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "ETH", 3000);

        // Act + Assert (ThrowsAny accepterer også underklasser, fx ArgumentNullException)
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert(fromName!, toName!, 1));
    }

    // --- Convert: ingen valutaer er oprettet endnu ---
    [Fact]
    public void Convert_IngenValutaerOprettet_KasterArgumentException() {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "ETH", 1));
    }

    // --- SetPricePerUnit: en ny pris overskriver den gamle ---
    [Theory]
    [InlineData(100, 200)]  // Prisen stiger
    [InlineData(200, 100)]  // Prisen falder
    [InlineData(100, 0)]    // Grænseværdi: ny pris er 0
    [InlineData(0, 100)]    // Grænseværdi: gammel pris var 0
    public void SetPricePerUnit_NyPris_OverskriverGammelPris(double oldPrice, double newPrice) {
        // Arrange: "USD" har pris 1, så omregning til den giver prisen i dollars
        var converter = CreateConverter("BTC", oldPrice, "USD", 1);

        // Act
        converter.SetPricePerUnit("BTC", newPrice);
        double result = converter.Convert("BTC", "USD", 1);

        // Assert
        Assert.Equal(newPrice, result, Precision);
    }

    // --- SetPricePerUnit: gyldige priser (0 og derover) accepteres ---
    [Theory]
    [InlineData(0)]               // Grænseværdi: laveste gyldige pris
    [InlineData(0.000001)]        // Lige over 0
    [InlineData(1)]
    [InlineData(60000)]
    [InlineData(double.MaxValue)] // Højeste mulige double
    public void SetPricePerUnit_GyldigPris_KasterIkke(double price) {
        // Arrange
        var converter = new Converter();

        // Act
        var exception = Record.Exception(() => converter.SetPricePerUnit("BTC", price));

        // Assert
        Assert.Null(exception);
    }

    // --- SetPricePerUnit: negative priser er ugyldige ---
    [Theory]
    [InlineData(-0.000001)]               // Grænseværdi: lige under 0
    [InlineData(-1)]
    [InlineData(-60000)]
    [InlineData(double.MinValue)]         // Laveste mulige double
    [InlineData(double.NegativeInfinity)]
    public void SetPricePerUnit_NegativPris_KasterArgumentException(double price) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit("BTC", price));
    }

    // --- SetPricePerUnit: en afvist pris må ikke overskrive den gamle ---
    [Fact]
    public void SetPricePerUnit_NegativPris_BevarerGammelPris() {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "USD", 1);

        // Act
        Record.Exception(() => converter.SetPricePerUnit("BTC", -1));
        double result = converter.Convert("BTC", "USD", 1);

        // Assert
        Assert.Equal(60000, result, Precision);
    }

    // =====================================================================
    // Egne antagelser: kommentarerne i Converter siger ikke noget om disse
    // tilfælde, så testene dokumenterer de valg, der er truffet.
    // =====================================================================

    // --- SetPricePerUnit: priser, der ikke er et endeligt tal, afvises ---
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SetPricePerUnit_IkkeEndeligPris_KasterArgumentException(double price) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit("BTC", price));
    }

    // --- SetPricePerUnit: tomme valutanavne afvises ---
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPricePerUnit_TomtNavn_KasterArgumentException(string? currencyName) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit(currencyName!, 100));
    }

    // --- Convert: negative eller ugyldige beløb afvises ---
    [Theory]
    [InlineData(-0.000001)]  // Grænseværdi: lige under 0
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Convert_UgyldigtBeloeb_KasterArgumentException(double amount) {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "ETH", 3000);

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "ETH", amount));
    }

    // --- Convert: omregning til en valuta med pris 0 ville give division med 0 ---
    [Fact]
    public void Convert_TilValutaMedPrisNul_KasterArgumentException() {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "WORTHLESS", 0);

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "WORTHLESS", 1));
    }
}
