using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoLib.Models
{
    public enum ActionType
    {
        StartProcess,
        Click,
        TypeText,
        VerifyElementExists,
        WindowOperation
    }

    public class AutomationStep
    {
        public string StepId { get; set; } = Guid.NewGuid().ToString();
        public ActionType Type { get; set; }
        
        public string AppPath { get; set; }
        public string Arguments { get; set; }
        
        public string AutomationId { get; set; }
        public string ElementName { get; set; }
        public List<int> TreePath { get; set; } = new List<int>();
        public string ControlType { get; set; } 

        public int? WindowX { get; set; }
        public int? WindowY { get; set; }
        public int? WindowWidth { get; set; }
        public int? WindowHeight { get; set; }
        public string WindowClassName { get; set; }
        public string ProcessName { get; set; }
        
        // NOWOŚĆ: Służy do odróżniania okien o identycznym tytule. 
        // Skrypt sprawdzi, czy wewnątrz okna znajduje się element o takiej nazwie.
        public string RequiredWindowContent { get; set; }
        
        public string Value { get; set; }
        
        public int TimeoutMs { get; set; } = 5000;
        public int DelayBeforeMs { get; set; } = 0;

        public List<AutomationStep> FallbackSteps { get; set; } = new List<AutomationStep>();
    }

    public class AutomationScript
    {
        public string ScriptName { get; set; }
        public List<AutomationStep> Steps { get; set; } = new List<AutomationStep>();
    }
}