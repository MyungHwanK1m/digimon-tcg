using System;
using System.IO;
using System.Linq;
using DigimonTcg.Core.Data;
using Xunit;

namespace DigimonTcg.Core.Tests;

public class SavedDeckStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "digimontcg-deckstore-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "saved_decks.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static (System.Collections.Generic.List<Models.CardData> Main, System.Collections.Generic.List<Models.CardData> Egg) RedDeck() =>
        StarterDecks.CreateSt1GaiaRedPreset();

    [Fact]
    public void Save_ShouldKeepSeveralNamedDecks_NewestFirst()
    {
        var store = new SavedDeckStore(FilePath);
        var (main, egg) = RedDeck();
        var (blueMain, blueEgg) = StarterDecks.CreateSt2CocytusBluePreset();

        store.Save("레드 비트", main, egg);
        store.Save("블루 컨트롤", blueMain, blueEgg);

        var decks = store.LoadAll();
        Assert.Equal(new[] { "블루 컨트롤", "레드 비트" }, decks.Select(d => d.Name));
        Assert.Equal(main.Count, decks[1].MainDeckCardIds.Count);
        Assert.Equal(egg.Count, decks[1].DigitamaCardIds.Count);
    }

    [Fact]
    public void Save_WithSameName_ShouldOverwriteIgnoringCaseAndSpaces()
    {
        var store = new SavedDeckStore(FilePath);
        var (main, egg) = RedDeck();
        store.Save("My Deck", main, egg);

        store.Save("  my deck ", main.Take(10).ToList(), egg);

        var deck = Assert.Single(store.LoadAll());
        Assert.Equal("my deck", deck.Name);
        Assert.Equal(10, deck.MainDeckCardIds.Count);
    }

    [Fact]
    public void Delete_ShouldRemoveOnlyThatDeck_AndRemoveFileWhenEmpty()
    {
        var store = new SavedDeckStore(FilePath);
        var (main, egg) = RedDeck();
        store.Save("A", main, egg);
        store.Save("B", main, egg);

        Assert.True(store.Delete("a"));
        Assert.Equal("B", Assert.Single(store.LoadAll()).Name);
        Assert.False(store.Delete("없는 덱"));

        Assert.True(store.Delete("B"));
        Assert.Empty(store.LoadAll());
        Assert.False(File.Exists(FilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")]   // 31자
    public void InvalidNames_ShouldBeRejected(string name)
    {
        Assert.NotNull(SavedDeckStore.ValidateName(name));
        var store = new SavedDeckStore(FilePath);
        var (main, egg) = RedDeck();
        Assert.Throws<ArgumentException>(() => store.Save(name, main, egg));
    }

    [Fact]
    public void LegacySingleDeckFile_ShouldStillLoad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ \"DeckName\": \"사용자 커스텀 덱\", \"DigitamaCardIds\": [\"ST1-01\"], \"MainDeckCardIds\": [\"ST1-02\", \"ST1-03\"] }");

        var deck = Assert.Single(new SavedDeckStore(FilePath).LoadAll());

        Assert.Equal("사용자 커스텀 덱", deck.Name);
        Assert.Equal(new[] { "ST1-02", "ST1-03" }, deck.MainDeckCardIds);
        Assert.Equal(new[] { "ST1-01" }, deck.DigitamaCardIds);
    }

    [Fact]
    public void Resolve_ShouldDropUnknownCardIdsAndReportThem()
    {
        var catalog = StarterDecks.GetAllCatalogCards().GroupBy(c => c.CardId).ToDictionary(g => g.Key, g => g.First());
        var deck = new SavedDeck { Name = "x", MainDeckCardIds = { "ST1-02", "NOPE-99" }, DigitamaCardIds = { "ST1-01" } };

        var (main, egg, missing) = SavedDeckStore.Resolve(deck, catalog);

        Assert.Single(main);
        Assert.Single(egg);
        Assert.Equal(1, missing);
    }

    [Fact]
    public void CorruptFile_ShouldLoadAsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(new SavedDeckStore(FilePath).LoadAll());
    }
}
