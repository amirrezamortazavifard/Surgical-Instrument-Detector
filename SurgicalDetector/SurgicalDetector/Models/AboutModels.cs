using System;
namespace SurgicalDetector.Models
{
    public class VersionHistoryItem
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public string Description { get; set; }
        public bool IsMajor { get; set; }
}

public class TechStackItem
    {
        public string Name { get; set; }
        public string IconData { get; set; }
        public string Description { get; set; }
    }

    public class SystemHealthItem
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string IconData { get; set; }
        public bool IsHealthy { get; set; }
    }

    public class TeamMemberItem
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Contribution { get; set; }
        public string GithubLink { get; set; }
        public string Initials { get; set; }
        public string ColorHex { get; set; }
    }
}