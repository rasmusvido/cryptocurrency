using Xunit;

// =========================================================================
// ÆKVIVALENSKLASSER
//
// Hver InlineData nedenfor er markeret med den eller de klasser, den
// repræsenterer, fx [GP2, GA2]. Grænseværdier er markeret med "grænse".
// Klasser markeret med * er egne antagelser: kommentarerne i Converter
// siger ikke noget om dem, så testene dokumenterer de valg, der er truffet.
//
// Pris (SetPricePerUnit: price)
//   Gyldige:   GP1  pris = 0                     (grænse)
//              GP2  pris > 0
//   Ugyldige:  UP1  pris < 0                     (inkl. -uendelig)
//              UP2* pris = NaN
//              UP3* pris = +uendelig
//
// Valutanavn (SetPricePerUnit: currencyName)
//   Gyldige:   GN1  ikke-tomt navn
//   Ugyldige:  UN1* null, "" eller kun mellemrum
//
// Valuta (Convert: fromCurrencyName / toCurrencyName)
//   Gyldige:   GV1  valutaen findes
//   Ugyldige:  UV1  valutaen findes ikke         (inkl. forkert store/små bogstaver)
//              UV2  null
//
// Beløb (Convert: amount)
//   Gyldige:   GA1  beløb = 0                    (grænse)
//              GA2  beløb > 0
//   Ugyldige:  UA1* beløb < 0
//              UA2* beløb = NaN eller uendelig
//
// Pris på til-valutaen (Convert)
//   Gyldige:   GT1  til-pris > 0
//   Ugyldige:  UT1* til-pris = 0                 (ville give division med 0)
//
// Forholdet mellem fra- og til-valuta (Convert)
//   Gyldige:   R1   forskellige valutaer med forskellige priser
//              R2   forskellige valutaer med samme pris
//              R3   samme valuta
// =========================================================================

public class ProgramTest {

    // Relativ tolerance ved sammenligning af decimaltal. Den er relativ (en andel af
    // det forventede tal) frem for et fast antal decimaler, så testene ikke fejler hvis
    // formlen skrives om, fx fra "amount * from / to" til "amount / to * from".
    private const double RelativeTolerance = 1e-9;

    private static void AssertAlmostEqual(double expected, double actual) {
        double tolerance = RelativeTolerance * Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Forventede {expected}, men fik {actual}.");
    }

    // Hjælpemetode: laver en Converter med to valutaer og deres priser i dollars.
    private static Converter CreateConverter(string fromName, double fromPrice, string toName, double toPrice) {
        var converter = new Converter();
        converter.SetPricePerUnit(fromName, fromPrice);
        converter.SetPricePerUnit(toName, toPrice);
        return converter;
    }

    // =====================================================================
    // GYLDIGE KLASSER: programmet skal regne rigtigt
    // =====================================================================

    // --- Convert: korrekt omregning mellem to forskellige valutaer ---
    [Theory]
    [InlineData(60000, 3000, 1, 20)]            // [R1, GP2, GA2, GT1] Dyr til billig valuta: 1 BTC (60000$) = 20 ETH (3000$)
    [InlineData(3000, 60000, 1, 0.05)]          // [R1, GP2, GA2, GT1] Billig til dyr valuta: 1 ETH = 0,05 BTC
    [InlineData(100, 50, 2.5, 5)]               // [R1, GP2, GA2, GT1] Beløb med decimaler
    [InlineData(0.5, 2, 10, 2.5)]               // [R1, GP2, GA2, GT1] Priser under 1 dollar
    [InlineData(100, 100, 3, 3)]                // [R2, GP2, GA2, GT1] Samme pris: beløbet skal være uændret
    [InlineData(60000, 3000, 0, 0)]             // [R1, GP2, GA1, GT1] Grænse: beløb 0 giver 0
    [InlineData(0, 100, 5, 0)]                  // [R1, GP1, GA2, GT1] Grænse: fra-valuta med pris 0 er værdiløs
    [InlineData(1, 0.00000095367431640625, 1, 1048576)] // [R1, GP2, GA2, GT1] Meget lille til-pris (2^-20) giver stort resultat
    [InlineData(1000000, 1, 1000, 1000000000)]  // [R1, GP2, GA2, GT1] Store tal
    public void Convert_ReturnererKorrektResultat(
        double fromPrice, double toPrice, double amount, double expected) {
        // Arrange
        var converter = CreateConverter("FROM", fromPrice, "TO", toPrice);

        // Act
        double result = converter.Convert("FROM", "TO", amount);

        // Assert
        AssertAlmostEqual(expected, result);
    }

    // --- Convert: omregning fra en valuta til sig selv ---
    [Theory]
    [InlineData(60000, 1)]    // [R3, GP2, GA2] Pris forskellig fra 1: fanger fejl hvor beløbet ganges med prisen
    [InlineData(60000, 2.5)]  // [R3, GP2, GA2] Beløb med decimaler
    [InlineData(60000, 0)]    // [R3, GP2, GA1] Grænse: beløb 0
    [InlineData(1, 1000)]     // [R3, GP2, GA2] Pris præcis 1
    [InlineData(0, 5)]        // [R3, GP1, GA2] Grænse: pris 0, samme valuta giver stadig samme beløb
    public void Convert_SammeValuta_ReturnererSammeBeloeb(double price, double amount) {
        // Arrange
        var converter = new Converter();
        converter.SetPricePerUnit("BTC", price);

        // Act
        double result = converter.Convert("BTC", "BTC", amount);

        // Assert
        AssertAlmostEqual(amount, result);
    }

    // --- Convert: omregning frem og tilbage giver det oprindelige beløb ---
    [Theory]
    [InlineData(60000, 3000, 1)]        // [R1, GP2, GA2, GT1]
    [InlineData(0.37, 12345.67, 89.1)]  // [R1, GP2, GA2, GT1] "Skæve" tal med mange decimaler
    public void Convert_FremOgTilbage_GiverOprindeligtBeloeb(double fromPrice, double toPrice, double amount) {
        // Arrange
        var converter = CreateConverter("FROM", fromPrice, "TO", toPrice);

        // Act
        double there = converter.Convert("FROM", "TO", amount);
        double back = converter.Convert("TO", "FROM", there);

        // Assert
        AssertAlmostEqual(amount, back);
    }

    // --- SetPricePerUnit: en ny pris overskriver den gamle ---
    [Theory]
    [InlineData(100, 200)]  // [GP2 -> GP2] Prisen stiger
    [InlineData(200, 100)]  // [GP2 -> GP2] Prisen falder
    [InlineData(100, 0)]    // [GP2 -> GP1] Grænse: ny pris er 0
    [InlineData(0, 100)]    // [GP1 -> GP2] Grænse: gammel pris var 0
    public void SetPricePerUnit_NyPris_OverskriverGammelPris(double oldPrice, double newPrice) {
        // Arrange: "USD" har pris 1, så omregning til den giver prisen i dollars
        var converter = CreateConverter("BTC", oldPrice, "USD", 1);

        // Act
        converter.SetPricePerUnit("BTC", newPrice);
        double result = converter.Convert("BTC", "USD", 1);

        // Assert
        AssertAlmostEqual(newPrice, result);
    }

    // --- SetPricePerUnit: gyldige priser gemmes og kan bruges ---
    [Theory]
    [InlineData(0)]               // [GP1] Grænse: laveste gyldige pris
    [InlineData(0.000001)]        // [GP2] Grænse: lige over 0
    [InlineData(1)]               // [GP2]
    [InlineData(60000)]           // [GP2]
    [InlineData(1e300)]           // [GP2] Meget stor pris
    public void SetPricePerUnit_GyldigPris_GemmesOgBruges(double price) {
        // Arrange
        var converter = new Converter();
        converter.SetPricePerUnit("USD", 1);

        // Act
        converter.SetPricePerUnit("BTC", price);
        double result = converter.Convert("BTC", "USD", 1);

        // Assert
        AssertAlmostEqual(price, result);
    }

    // =====================================================================
    // UGYLDIGE KLASSER: programmet skal afvise input og ikke ændre tilstand
    // =====================================================================

    // --- Convert: ukendt valuta skal kaste ArgumentException ---
    [Theory]
    [InlineData("BTC", "XXX")]  // [GV1, UV1] Til-valuta findes ikke
    [InlineData("XXX", "BTC")]  // [UV1, GV1] Fra-valuta findes ikke
    [InlineData("XXX", "YYY")]  // [UV1, UV1] Ingen af valutaerne findes
    [InlineData("btc", "ETH")]  // [UV1, GV1] Navne skelner mellem store og små bogstaver
    [InlineData(null, "BTC")]   // [UV2, GV1] Null som fra-valuta
    [InlineData("BTC", null)]   // [GV1, UV2] Null som til-valuta
    public void Convert_UkendtValuta_KasterArgumentException(string? fromName, string? toName) {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "ETH", 3000);

        // Act + Assert (ThrowsAny accepterer også underklasser, fx ArgumentNullException)
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert(fromName!, toName!, 1));
    }

    // --- Convert: ingen valutaer er oprettet endnu ---
    [Fact]
    public void Convert_IngenValutaerOprettet_KasterArgumentException() { // [UV1, UV1]
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "ETH", 1));
    }

    // --- SetPricePerUnit: negative priser er ugyldige ---
    [Theory]
    [InlineData(-0.000001)]               // [UP1] Grænse: lige under 0
    [InlineData(-1)]                      // [UP1]
    [InlineData(-60000)]                  // [UP1]
    [InlineData(double.MinValue)]         // [UP1] Laveste mulige double
    [InlineData(double.NegativeInfinity)] // [UP1]
    public void SetPricePerUnit_NegativPris_KasterArgumentException(double price) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit("BTC", price));
    }

    // --- SetPricePerUnit: en afvist pris må ikke overskrive en eksisterende pris ---
    [Theory]
    [InlineData(-1)]          // [UP1]
    [InlineData(double.NaN)]  // [UP2*]
    public void SetPricePerUnit_UgyldigPris_BevarerGammelPris(double invalidPrice) {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "USD", 1);

        // Act
        Record.Exception(() => converter.SetPricePerUnit("BTC", invalidPrice));
        double result = converter.Convert("BTC", "USD", 1);

        // Assert
        AssertAlmostEqual(60000, result);
    }

    // --- SetPricePerUnit: en afvist pris må ikke oprette en ny valuta ---
    [Theory]
    [InlineData(-1)]          // [UP1]
    [InlineData(double.NaN)]  // [UP2*]
    public void SetPricePerUnit_UgyldigPris_OpretterIkkeValuta(double invalidPrice) {
        // Arrange
        var converter = new Converter();
        converter.SetPricePerUnit("USD", 1);

        // Act
        Record.Exception(() => converter.SetPricePerUnit("NEW", invalidPrice));

        // Assert: valutaen findes ikke, så Convert skal afvise den
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("NEW", "USD", 1));
    }

    // --- SetPricePerUnit: priser, der ikke er et endeligt tal, afvises ---
    [Theory]
    [InlineData(double.NaN)]              // [UP2*]
    [InlineData(double.PositiveInfinity)] // [UP3*]
    public void SetPricePerUnit_IkkeEndeligPris_KasterArgumentException(double price) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit("BTC", price));
    }

    // --- SetPricePerUnit: tomme valutanavne afvises ---
    [Theory]
    [InlineData((string?)null)]  // [UN1*] null
    [InlineData("")]             // [UN1*] tom streng
    [InlineData("   ")]          // [UN1*] kun mellemrum
    public void SetPricePerUnit_TomtNavn_KasterArgumentException(string? currencyName) {
        // Arrange
        var converter = new Converter();

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.SetPricePerUnit(currencyName!, 100));
    }

    // --- Convert: negative eller ikke-endelige beløb afvises ---
    [Theory]
    [InlineData(-0.000001)]               // [UA1*] Grænse: lige under 0
    [InlineData(-1)]                      // [UA1*]
    [InlineData(double.NaN)]              // [UA2*]
    [InlineData(double.PositiveInfinity)] // [UA2*]
    public void Convert_UgyldigtBeloeb_KasterArgumentException(double amount) {
        // Arrange
        var converter = CreateConverter("BTC", 60000, "ETH", 3000);

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "ETH", amount));
    }

    // --- Convert: omregning til en valuta med pris 0 ville give division med 0 ---
    [Fact]
    public void Convert_TilValutaMedPrisNul_KasterArgumentException() { // [UT1*]
        // Arrange
        var converter = CreateConverter("BTC", 60000, "WORTHLESS", 0);

        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => converter.Convert("BTC", "WORTHLESS", 1));
    }
}
