using System;
using InsaitTextEditor.Models;
using LiteDB;

namespace InsaitTextEditor.Services;

public class UserInstructionService
{
    private readonly DatabaseService _databaseService;
    private UserInstruction? _cachedInstruction;

    public UserInstructionService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public UserInstruction? GetUserInstruction()
    {
        if (_cachedInstruction != null)
            return _cachedInstruction;

        var collection = _databaseService.GetCollection<UserInstruction>("UserInstruction");
        // Get the single instruction document (if any)
        _cachedInstruction = collection.FindOne(x => true);

        return _cachedInstruction;
    }

    public void SaveUserInstruction(UserInstruction instruction)
    {
        instruction.LastModified = DateTime.UtcNow;

        var collection = _databaseService.GetCollection<UserInstruction>("UserInstruction");

        // Remove any previous instructions (there should be only one entry)
        collection.DeleteAll();

        // Insert the new instruction
        collection.Insert(instruction);
        collection.EnsureIndex(x => x.LastModified);

        _cachedInstruction = instruction;
    }

    public void UpdatePerformanceSettings(int contextSize, int maxTokens)
    {
        var instruction = GetUserInstruction() ?? new UserInstruction();
        instruction.ContextSize = contextSize;
        instruction.MaxTokens = maxTokens;
        SaveUserInstruction(instruction);
    }
}