namespace razorpm.Models
{
    public class ReportRow
    {
        public string Employee { get; set; } = "";
        public int TotalTasks { get; set; }
        public int ActiveTasks { get; set; }
        public int DoneTasks { get; set; }
        public int TotalHours { get; set; }
    }
}
