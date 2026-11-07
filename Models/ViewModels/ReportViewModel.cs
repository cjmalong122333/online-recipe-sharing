using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;
using System;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class ReportViewModel
    {
        public Report Report { get; set; }
        public string RecipeTitle { get; set; }
        public string ReporterUsername { get; set; }
        public string RecipeId { get; set; }
    }

    public class ReportFilterViewModel
    {
        public List<ReportViewModel> Reports { get; set; }
        public string SelectedStatus { get; set; }
        public long TotalReports { get; set; }
        public long PendingReports { get; set; }
        public long ReviewedReports { get; set; }
        public long DismissedReports { get; set; }
        public long ResolvedReports { get; set; }
    }

    public class ReportStatsViewModel
    {
        public long TotalReports { get; set; }
        public long PendingReports { get; set; }
        public long ReviewedReports { get; set; }
        public long DismissedReports { get; set; }
        public long ResolvedReports { get; set; }
        public List<ReportReasonCount> ReportsByReason { get; set; }
        public List<ReportMonthlyCount> ReportsByMonth { get; set; }
    }

    public class ReportReasonCount
    {
        public string Reason { get; set; }
        public long Count { get; set; }
    }

    public class ReportMonthlyCount
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public long Count { get; set; }
    }
}