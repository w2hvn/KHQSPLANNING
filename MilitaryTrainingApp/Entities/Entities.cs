using System;
using System.Collections.Generic;

namespace MilitaryTrainingApp.Entities
{
    public class Plan
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Year { get; set; }
        public string Status { get; set; } = "DRAFT";
        public DateTime? CreatedAt { get; set; }

        public ICollection<PlanTarget> PlanTargets { get; set; } = new List<PlanTarget>();
        public ICollection<TimeNode> TimeNodes { get; set; } = new List<TimeNode>();
        public ICollection<BlackoutDate> BlackoutDates { get; set; } = new List<BlackoutDate>();
        public ICollection<SchedulingPriorityRule> SchedulingPriorityRules { get; set; } = new List<SchedulingPriorityRule>();
    }

    public class BlackoutDate
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;
        public string HolidayName { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Description { get; set; }
    }

    public class SchedulingPriorityRule
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;
        public string RuleCode { get; set; } = null!;
        public string RuleName { get; set; } = null!;
        public int PriorityScore { get; set; } = 50;
        public bool IsActive { get; set; } = true;
    }

    public class TrainingTarget
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int? SortOrder { get; set; }

        public ICollection<PlanTarget> PlanTargets { get; set; } = new List<PlanTarget>();
    }

    public class PlanTarget
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;
        public int TargetId { get; set; }
        public TrainingTarget Target { get; set; } = null!;

        public int DaysPerWeek { get; set; } = 5;
        public decimal MorningHours { get; set; } = 4.0m;
        public decimal AfternoonHours { get; set; } = 3.0m;
        public decimal NightHours { get; set; } = 2.0m;

        public ICollection<ProgramNode> ProgramNodes { get; set; } = new List<ProgramNode>();
    }

    public class ProgramNodeType
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int DepthLevel { get; set; }

        public ICollection<ProgramNode> ProgramNodes { get; set; } = new List<ProgramNode>();
    }

    public class TimeNodeType
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int DepthLevel { get; set; }

        public ICollection<TimeNode> TimeNodes { get; set; } = new List<TimeNode>();
    }

    public class ProgramNode
    {
        public int Id { get; set; }
        public int PlanTargetId { get; set; }
        public PlanTarget PlanTarget { get; set; } = null!;
        public int? ParentId { get; set; }
        public ProgramNode? Parent { get; set; }
        public int NodeTypeId { get; set; }
        public ProgramNodeType NodeType { get; set; } = null!;
        public string? Code { get; set; }
        public string Name { get; set; } = null!;
        public decimal Capacity { get; set; } = 0.00m;
        public int Level { get; set; } = 1;
        public string? TreePath { get; set; }
        public int? SortOrder { get; set; }

        public int ComplexityLevel { get; set; } = 0;
        public bool IsNightTraining { get; set; } = false;
        public bool IsOutdoor { get; set; } = false;
        public bool IsHeavyPhysical { get; set; } = false;
        public int? PrerequisiteNodeId { get; set; }
        public int? TimeNodeId { get; set; }

        public virtual TimeNode? TimeNode { get; set; }
        public ProgramNode? PrerequisiteNode { get; set; }
        public ProgramNodeDecor? Decor { get; set; }
        public ICollection<ProgramNode> Children { get; set; } = new List<ProgramNode>();
        public ICollection<TimeAllocation> TimeAllocations { get; set; } = new List<TimeAllocation>();
    }

    public class TimeNode
    {
        public int Id { get; set; }
        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;
        public int? ParentId { get; set; }
        public TimeNode? Parent { get; set; }
        public int NodeTypeId { get; set; }
        public TimeNodeType NodeType { get; set; } = null!;
        public string? Code { get; set; }
        public string Name { get; set; } = null!;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Level { get; set; } = 1;
        public string? TreePath { get; set; }
        public int? SortOrder { get; set; }

        public bool IsManual { get; set; } = false;
        public bool IsLocked { get; set; } = false;

        public ICollection<TimeNode> Children { get; set; } = new List<TimeNode>();
        public ICollection<TimeAllocation> TimeAllocations { get; set; } = new List<TimeAllocation>();
    }

    public class TimeAllocation
    {
        public int Id { get; set; }
        public int ProgramNodeId { get; set; }
        public ProgramNode ProgramNode { get; set; } = null!;
        public int TimeNodeId { get; set; }
        public TimeNode TimeNode { get; set; } = null!;
        public decimal AllocatedHours { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class ProgramNodeDecor
    {
        public int ProgramNodeId { get; set; }
        public ProgramNode ProgramNode { get; set; } = null!;
        public string BgColorHex { get; set; } = "#FFFFFF";
        public string BorderColorHex { get; set; } = "#0066CC";
        public string TextColorHex { get; set; } = "#000000";
        public DateTime? UpdatedAt { get; set; }
    }
}