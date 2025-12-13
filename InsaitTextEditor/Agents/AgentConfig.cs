using System.Collections.Generic;

namespace InsaitTextEditor.Agents;

public class AgentConfig
{
    public string AgentName => "Insait Creative Assistant";
    public string AgentDescription => "Асистент для створення текстів та віршів з автоматичним збереженням";
    public List<string> AvailableTools => ["save_to_file"];
    public int MaxIterations => 5;
    public bool EnableToolUse => true;
    
    // ✅ НОВИЙ параметр для автоматичного збереження
    public bool AutoSaveCreativeContent => true;
    
    // Тригери для автоматичного збереження
    public List<string> AutoSaveTriggers => new()
    {
        "напиши вірш",
        "створи вірш",
        "скомпонуй вірш",
        "напиши історію",
        "створи оповідання",
        "згенеруй текст",
        "напиши есе",
        "напиши оповідання",
        // Додаткові тригери без "напиши/створи"
        "вірш про",
        "вірш ",
        "історія про",
        "оповідання про",
        "текст про",
        "есе про"
    };
}