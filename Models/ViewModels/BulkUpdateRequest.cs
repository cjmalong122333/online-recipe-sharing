using System.Collections.Generic;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class BulkUpdateRequest
    {
        public List<string> ReportIds { get; set; }
        public string Status { get; set; }
    }
}