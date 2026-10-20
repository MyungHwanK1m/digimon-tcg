using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Data;

/// <summary>이름을 붙여 저장한 덱 1개 (카드는 카드 번호로 저장한다)</summary>
public class SavedDeck
{
    public string Name { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
    public List<string> MainDeckCardIds { get; set; } = new();
    public List<string> DigitamaCardIds { get; set; } = new();
}

/// <summary>
/// 내가 만든 덱 보관함 (data/saved_decks.json, 로컬 전용).
/// 덱마다 이름을 붙여 여러 개 저장하고, 같은 이름으로 저장하면 덮어쓴다 (대소문자 · 앞뒤 공백 무시).
/// 예전 형식(덱 1개만 저장된 파일)도 그대로 읽는다.
/// </summary>
public class SavedDeckStore
{
    public const int MaxNameLength = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // 한글 덱 이름을 그대로 읽을 수 있게
    };

    private readonly string _filePath;

    public SavedDeckStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>저장할 수 있는 이름인지 (빈 이름 · 너무 긴 이름 불가). 문제가 없으면 null</summary>
    public static string? ValidateName(string? name)
    {
        string n = NormalizeName(name);
        if (n.Length == 0)
            return "덱 이름을 입력하세요.";
        if (n.Length > MaxNameLength)
            return $"덱 이름은 {MaxNameLength}자까지 쓸 수 있습니다.";
        return null;
    }

    /// <summary>저장된 덱 전체 (최근 저장한 순서)</summary>
    public IReadOnlyList<SavedDeck> LoadAll()
    {
        if (!File.Exists(_filePath))
            return Array.Empty<SavedDeck>();

        try
        {
            string json = File.ReadAllText(_filePath);
            using var doc = JsonDocument.Parse(json);
            List<SavedDeck> decks;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                decks = JsonSerializer.Deserialize<List<SavedDeck>>(json, JsonOptions) ?? new();
            }
            else
            {
                // 예전 형식: { "DeckName": ..., "MainDeckCardIds": [...], "DigitamaCardIds": [...] }
                var legacy = doc.RootElement;
                decks = new List<SavedDeck>
                {
                    new()
                    {
                        Name = legacy.TryGetProperty("DeckName", out var n) ? n.GetString() ?? "내 덱" : "내 덱",
                        SavedAt = File.GetLastWriteTime(_filePath),
                        MainDeckCardIds = ReadIds(legacy, "MainDeckCardIds"),
                        DigitamaCardIds = ReadIds(legacy, "DigitamaCardIds")
                    }
                };
            }
            return decks.Where(d => NormalizeName(d.Name).Length > 0)
                        .OrderByDescending(d => d.SavedAt)
                        .ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return Array.Empty<SavedDeck>();
        }
    }

    private static List<string> ReadIds(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().Select(e => e.GetString()).Where(s => s != null).Select(s => s!).ToList()
            : new List<string>();

    public SavedDeck? Find(string? name)
    {
        string n = NormalizeName(name);
        return LoadAll().FirstOrDefault(d => string.Equals(NormalizeName(d.Name), n, StringComparison.OrdinalIgnoreCase));
    }

    public bool Exists(string? name) => Find(name) != null;

    /// <summary>이름을 붙여 저장한다. 같은 이름의 덱이 있으면 덮어쓴다.</summary>
    public SavedDeck Save(string name, IReadOnlyList<CardData> mainDeck, IReadOnlyList<CardData> digitamaDeck)
    {
        string? error = ValidateName(name);
        if (error != null)
            throw new ArgumentException(error, nameof(name));

        string n = NormalizeName(name);
        var decks = LoadAll()
            .Where(d => !string.Equals(NormalizeName(d.Name), n, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var saved = new SavedDeck
        {
            Name = n,
            SavedAt = DateTime.Now,
            MainDeckCardIds = mainDeck.Select(c => c.CardId).ToList(),
            DigitamaCardIds = digitamaDeck.Select(c => c.CardId).ToList()
        };
        decks.Insert(0, saved);
        WriteAll(decks);
        return saved;
    }

    /// <summary>이름으로 덱을 지운다. 지웠으면 true</summary>
    public bool Delete(string? name)
    {
        string n = NormalizeName(name);
        var decks = LoadAll().ToList();
        int removed = decks.RemoveAll(d => string.Equals(NormalizeName(d.Name), n, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;
        if (decks.Count == 0)
            File.Delete(_filePath);
        else
            WriteAll(decks);
        return true;
    }

    /// <summary>저장된 카드 번호를 도감 카드로 바꾼다 (도감에 없는 카드 번호는 빼고, 뺀 장수를 돌려준다)</summary>
    public static (List<CardData> MainDeck, List<CardData> DigitamaDeck, int MissingCards) Resolve(
        SavedDeck deck, IReadOnlyDictionary<string, CardData> catalogById)
    {
        var main = deck.MainDeckCardIds.Where(catalogById.ContainsKey).Select(id => catalogById[id]).ToList();
        var digitama = deck.DigitamaCardIds.Where(catalogById.ContainsKey).Select(id => catalogById[id]).ToList();
        int missing = deck.MainDeckCardIds.Count + deck.DigitamaCardIds.Count - main.Count - digitama.Count;
        return (main, digitama, missing);
    }

    private void WriteAll(List<SavedDeck> decks)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(decks, JsonOptions));
    }
}
