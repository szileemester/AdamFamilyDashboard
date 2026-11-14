using AdaManagement.Shared;

namespace AdaManagement.Tests;

/// <summary>A szakik elérhetőségeinek ellenőrzése és linkké alakítása.</summary>
public class ContactLinksTests
{
    [Theory]
    [InlineData("szaki.hu", "https://szaki.hu/")]
    [InlineData("  www.szaki.hu/arak ", "https://www.szaki.hu/arak")]
    [InlineData("http://szaki.hu", "http://szaki.hu/")]
    [InlineData("szaki.hu/ajanlat?vissza=https://szaki.hu", "https://szaki.hu/ajanlat?vissza=https://szaki.hu")]
    [InlineData("HTTPS://Szaki.hu", "https://szaki.hu/")]
    [InlineData("https://", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ftp://szaki.hu", null)]
    [InlineData("szaki", null)]
    [InlineData("", null)]
    public void Weboldal_csak_http_vagy_https(string input, string? expected) =>
        Assert.Equal(expected, ContactLinks.WebsiteUrl(input));

    [Theory]
    [InlineData("https://www.szaki.hu/", "szaki.hu")]
    [InlineData("szaki.hu/arak/", "szaki.hu/arak")]
    [InlineData("festes.hu/árak", "festes.hu/árak")] // nem „%C3%A1rak”
    public void Weboldal_rovid_felirata(string input, string expected) =>
        Assert.Equal(expected, ContactLinks.WebsiteLabel(input));

    [Theory]
    [InlineData("+36 30 / 123-4567", true)]
    [InlineData("(06-1) 234 5678", true)]
    [InlineData("12345", false)]     // túl rövid
    [InlineData("hívj fel", false)]
    public void Telefonszam(string input, bool valid) => Assert.Equal(valid, ContactLinks.IsValidPhone(input));

    [Fact]
    public void Telefon_link_csak_szamjegy_es_plusz() =>
        Assert.Equal("tel:+36301234567", ContactLinks.PhoneHref("+36 30 / 123-4567"));

    [Theory]
    [InlineData("bela@pelda.hu", true)]
    [InlineData("bela@pelda", false)]
    [InlineData("bela pelda.hu", false)]
    public void Email(string input, bool valid) => Assert.Equal(valid, ContactLinks.IsValidEmail(input));
}
