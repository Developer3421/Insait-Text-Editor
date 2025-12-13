using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Services.Database.Specialized;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Шукає релевантні факти за запитом (спрощений семантичний пошук)
/// </summary>
public class MemoryQueryEngine
{
    private readonly MemoryDatabaseService _memoryDb;

    public MemoryQueryEngine(MemoryDatabaseService memoryDb)
    {
        _memoryDb = memoryDb;
    }

    public async Task<List<MemoryFact>> SearchAsync(string query, int maxResults = 5)
    {
        // Витягти ключові слова з запиту
        var keywords = ExtractKeywords(query);
        
        // Шукати по тегах
        var factsByTags = _memoryDb.GetFactsByTags(keywords);
        
        // Якщо нічого не знайдено, повернути останні факти
        if (!factsByTags.Any())
        {
            return _memoryDb.GetActiveFacts().Take(maxResults).ToList();
        }

        // Ранжувати за релевантністю
        var rankedFacts = factsByTags
            .Select(fact => new
            {
                Fact = fact,
                Score = CalculateRelevanceScore(fact, keywords)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Fact)
            .ToList();

        return await Task.FromResult(rankedFacts);
    }

    private List<string> ExtractKeywords(string query)
    {
        // Спрощене витягування ключових слів
        var words = query.ToLower()
            .Split(' ', ',', '.', '!', '?')
            .Where(w => w.Length > 3) // Ігноруємо короткі слова
            .Distinct()
            .ToList();

        return words;
    }

    private double CalculateRelevanceScore(MemoryFact fact, List<string> keywords)
    {
        double score = 0;

        // +1 за кожне співпадіння тегу
        var matchingTags = fact.Tags.Intersect(keywords).Count();
        score += matchingTags * 2.0;

        // +0.5 якщо ключове слово є в content
        foreach (var keyword in keywords)
        {
            if (fact.Content.ToLower().Contains(keyword))
                score += 0.5;
        }

        // Бонус за впевненість
        score *= fact.Confidence;

        // Бонус за свіжість (нещодавно використовувані факти)
        var daysSinceAccess = (System.DateTime.UtcNow - fact.LastAccessedAt).TotalDays;
        if (daysSinceAccess < 7)
            score *= 1.2;

        return score;
    }
}
