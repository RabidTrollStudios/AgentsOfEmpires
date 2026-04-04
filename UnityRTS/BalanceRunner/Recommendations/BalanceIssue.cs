using System.Collections.Generic;

namespace BalanceRunner.Recommendations
{
    /// <summary>
    /// A detected balance issue with severity, evidence, and optional tuning suggestion.
    /// </summary>
    public class BalanceIssue
    {
        public IssueSeverity Severity { get; set; }
        public IssueCategory Category { get; set; }
        public string Description { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();

        /// <summary>Optional: specific constant to adjust.</summary>
        public string ConstantName { get; set; }

        /// <summary>Optional: current value of the constant.</summary>
        public float? CurrentValue { get; set; }

        /// <summary>Optional: suggested new value.</summary>
        public float? SuggestedValue { get; set; }

        /// <summary>Optional: why this adjustment is recommended.</summary>
        public string Rationale { get; set; }

        /// <summary>Optional: scenario to re-test after applying the change.</summary>
        public string FollowUpTest { get; set; }
    }

    public enum IssueSeverity
    {
        Critical,
        High,
        Medium,
        Low
    }

    public enum IssueCategory
    {
        Dominance,
        UnitViability,
        CostEfficiency,
        TimingWindow,
        CounterBalance
    }
}
