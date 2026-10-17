using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Core.Data;

public class SavedDeckModel
{
    public string DeckName { get; set; } = "커스텀 덱";
    public List<string> DigitamaCardIds { get; set; } = new();
    public List<string> MainDeckCardIds { get; set; } = new();
}

public class CardCatalogService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _cacheFilePath;
    private readonly string _savedDecksFilePath;
    private readonly string _imagesDirPath;
    private List<CardData> _catalog = new();

    public IReadOnlyList<CardData> Catalog => _catalog;
    public string ImagesDirectoryPath => _imagesDirPath;

    public CardCatalogService(string? dataDirectory = null)
    {
        string baseDir = dataDirectory ?? FindDataDirectory();
        Directory.CreateDirectory(baseDir);
        _imagesDirPath = Path.Combine(baseDir, "images");
        Directory.CreateDirectory(_imagesDirPath);
        _cacheFilePath = Path.Combine(baseDir, "cards_cache.json");
        _savedDecksFilePath = Path.Combine(baseDir, "saved_decks.json");
        LoadOrInitializeCache();
    }

    private static string FindDataDirectory()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(current);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "images")) || File.Exists(Path.Combine(dir.FullName, "DigimonTcg.sln")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        const string projectDataPath = @"d:\Project\DigimonTcg\data";
        if (Directory.Exists(projectDataPath))
        {
            return projectDataPath;
        }

        return Path.Combine(Environment.CurrentDirectory, "data");
    }

    public void LoadOrInitializeCache()
    {
        var defaultCards = StarterDecks.GetAllCatalogCards();
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                string json = File.ReadAllText(_cacheFilePath);
                var loaded = JsonSerializer.Deserialize<List<CardData>>(json, JsonOptions);
                if (loaded != null && loaded.Count > 0)
                {
                    // 구버전 임시 ID 제거 및 최신 기본 카드/이미지 URL 병합
                    var merged = loaded
                        .Where(c => !c.CardId.EndsWith("B", StringComparison.Ordinal))
                        .ToDictionary(c => c.CardId);

                    foreach (var def in defaultCards)
                    {
                        merged[def.CardId] = def;
                    }
                    _catalog = merged.Values.OrderBy(c => c.CardId).ToList();
                    SaveCache();
                    return;
                }
            }
            catch
            {
                // 캐시 손상 시 기본 카드로 복구
            }
        }

        _catalog = defaultCards.OrderBy(c => c.CardId).ToList();
        SaveCache();
    }

    public void SaveCache()
    {
        string json = JsonSerializer.Serialize(_catalog, JsonOptions);
        File.WriteAllText(_cacheFilePath, json);
    }

    public string GetLocalImageFilePath(CardData card)
    {
        return Path.Combine(_imagesDirPath, $"{card.CardId}.jpg");
    }

    /// <summary>
    /// 카드 이미지를 로컬 data/images/{CardId}.jpg 에 캐싱하고 경로 반환
    /// </summary>
    public async Task<string?> EnsureCardImageCachedAsync(CardData card)
    {
        string localPath = GetLocalImageFilePath(card);
        if (File.Exists(localPath) && new FileInfo(localPath).Length > 500)
        {
            return localPath;
        }

        string url = !string.IsNullOrWhiteSpace(card.ImageUrl)
            ? card.ImageUrl
            : $"https://images.digimoncard.io/images/cards/{card.CardId}.jpg";

        try
        {
            byte[] bytes = await Http.GetByteArrayAsync(url);
            if (bytes.Length > 500)
            {
                await File.WriteAllBytesAsync(localPath, bytes);
                return localPath;
            }
        }
        catch
        {
            // 오프라인 시 프로시저럴 카드 일러스트 프레임으로 대체 렌더링
        }
        return null;
    }

    /// <summary>
    /// DigimonCard.io 공개 REST API 연동 후 로컬 cards_cache.json 갱신
    /// </summary>
    public async Task<(bool Success, string Message)> SyncFromDigimonCardApiAsync(string queryCardNumber = "ST1")
    {
        try
        {
            string url = $"https://digimoncard.io/api-public/search.php?card={Uri.EscapeDataString(queryCardNumber)}&series=Digimon%20Card%20Game";
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (false, "API 응답 형식이 배열이 아닙니다. 오프라인 캐시를 유지합니다.");

            int syncedCount = 0;
            var map = _catalog.ToDictionary(c => c.CardId);

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                string? cardNum = elem.TryGetProperty("cardnumber", out var cn) ? cn.GetString() : null;
                string? name = elem.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                if (string.IsNullOrWhiteSpace(cardNum) || string.IsNullOrWhiteSpace(name))
                    continue;

                string imageUrl = elem.TryGetProperty("image_url", out var img)
                    ? img.GetString() ?? $"https://images.digimoncard.io/images/cards/{cardNum}.jpg"
                    : $"https://images.digimoncard.io/images/cards/{cardNum}.jpg";

                if (map.TryGetValue(cardNum, out var existing))
                {
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        existing.ImageUrl = imageUrl;
                    syncedCount++;
                }
                else
                {
                    var newCard = new CardData
                    {
                        CardId = cardNum,
                        Name = name,
                        Type = ParseCardType(elem.TryGetProperty("type", out var tp) ? tp.GetString() : null),
                        Color = ParseCardColor(elem.TryGetProperty("color", out var cl) ? cl.GetString() : null),
                        Level = elem.TryGetProperty("level", out var lv) && lv.ValueKind == JsonValueKind.Number ? lv.GetInt32() : 3,
                        PlayCost = elem.TryGetProperty("play_cost", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : 3,
                        EvoCost = elem.TryGetProperty("evolution_cost", out var ec) && ec.ValueKind == JsonValueKind.Number ? ec.GetInt32() : 0,
                        EvoRequirementLevel = elem.TryGetProperty("level", out var lv2) && lv2.ValueKind == JsonValueKind.Number ? Math.Max(2, lv2.GetInt32() - 1) : 2,
                        EvoRequirementColor = ParseCardColor(elem.TryGetProperty("color", out var cl2) ? cl2.GetString() : null),
                        DP = elem.TryGetProperty("dp", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetInt32() : 2000,
                        EffectText = elem.TryGetProperty("maineffect", out var me) ? me.GetString() ?? "" : "",
                        InheritedEffectText = elem.TryGetProperty("soureeffect", out var se) ? se.GetString() ?? "" : "",
                        ImageUrl = imageUrl
                    };
                    map[cardNum] = newCard;
                    syncedCount++;
                }
            }

            _catalog = map.Values.OrderBy(c => c.CardId).ToList();
            SaveCache();
            return (true, $"DigimonCard.io API 동기화 완료 ({syncedCount}종 업데이트 및 cards_cache.json 저장 완료)");
        }
        catch (Exception ex)
        {
            return (false, $"네트워크 연결 불가 또는 API 응답 지연 ({ex.Message}). 로컬 cards_cache.json 오프라인 캐시({_catalog.Count}종)로 동작합니다.");
        }
    }

    public List<CardData> SearchCards(
        string? keyword = null,
        CardColor? color = null,
        CardType? type = null,
        int? level = null,
        int? maxPlayCost = null)
    {
        IEnumerable<CardData> query = _catalog;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string kw = keyword.Trim();
            query = query.Where(c =>
                c.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.CardId.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.EffectText.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.InheritedEffectText.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        if (color.HasValue)
            query = query.Where(c => c.Color == color.Value);

        if (type.HasValue)
            query = query.Where(c => c.Type == type.Value);

        if (level.HasValue)
            query = query.Where(c => c.Level == level.Value);

        if (maxPlayCost.HasValue)
            query = query.Where(c => c.PlayCost <= maxPlayCost.Value);

        return query.OrderBy(c => c.CardId).ToList();
    }

    public void SaveCustomDeck(string deckName, IReadOnlyList<CardData> mainDeck, IReadOnlyList<CardData> digitamaDeck)
    {
        var model = new SavedDeckModel
        {
            DeckName = deckName,
            MainDeckCardIds = mainDeck.Select(c => c.CardId).ToList(),
            DigitamaCardIds = digitamaDeck.Select(c => c.CardId).ToList()
        };
        string json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(_savedDecksFilePath, json);
    }

    public (string DeckName, List<CardData> MainDeck, List<CardData> DigitamaDeck)? LoadCustomDeck()
    {
        if (!File.Exists(_savedDecksFilePath))
            return null;

        try
        {
            string json = File.ReadAllText(_savedDecksFilePath);
            var model = JsonSerializer.Deserialize<SavedDeckModel>(json, JsonOptions);
            if (model == null)
                return null;

            var map = _catalog.ToDictionary(c => c.CardId);
            var main = model.MainDeckCardIds.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToList();
            var digitama = model.DigitamaCardIds.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToList();
            return (model.DeckName, main, digitama);
        }
        catch
        {
            return null;
        }
    }

    private static CardType ParseCardType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "digi-egg" or "digitama" => CardType.Digitama,
        "tamer" => CardType.Tamer,
        "option" => CardType.Option,
        _ => CardType.Digimon
    };

    private static CardColor ParseCardColor(string? raw) => raw?.ToLowerInvariant() switch
    {
        "blue" => CardColor.Blue,
        "yellow" => CardColor.Yellow,
        "green" => CardColor.Green,
        "black" => CardColor.Black,
        "purple" => CardColor.Purple,
        "white" => CardColor.White,
        _ => CardColor.Red
    };
}
