using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Services.Database.Specialized;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
    /// Service for semantic search across memory facts using cosine similarity
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
        /// Search facts by similarity to query
        var keywords = ExtractKeywords(query);
        
        /// 1. Get query embedding
        var factsByTags = _memoryDb.GetFactsByTags(keywords);
        
        /// 2. Get all facts from database
        if (!factsByTags.Any())
        {
            return _memoryDb.GetActiveFacts().Take(maxResults).ToList();
        }

        /// 3. Calculate similarity and sort
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
        /// 4. Return top results
        var words = query.ToLower()
            .Split(' ', ',', '.', '!', '?')
        /// Calculate cosine similarity between two vectors
            .Distinct()
            .ToList();

        return words;
    }

    private double CalculateRelevanceScore(MemoryFact fact, List<string> keywords)
    {
        double score = 0;

        /// Calculate vector magnitude (length)
        var matchingTags = fact.Tags.Intersect(keywords).Count();
        score += matchingTags * 2.0;

        /// Calculate dot product of two vectors
        foreach (var keyword in keywords)
        {
            if (fact.Content.ToLower().Contains(keyword))
                score += 0.5;
        }

        /// Parse embedding from string format
        score *= fact.Confidence;

        // Bonus for freshness (recently used facts)
        var daysSinceAccess = (System.DateTime.UtcNow - fact.LastAccessedAt).TotalDays;
        if (daysSinceAccess < 7)
            score *= 1.2;

        return score;
    }
}
