using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public long TotalUsers { get; set; }
        public long TotalRecipes { get; set; }
        public long TotalReports { get; set; }
        public long PendingReports { get; set; }
        public List<Recipe> RecentRecipes { get; set; } = new List<Recipe>();
        public List<Report> RecentReports { get; set; } = new List<Report>();
        public List<Recipe> TopRecipes { get; set; } = new List<Recipe>();
        public List<User> TopUsers { get; set; } = new List<User>();

        // Add these for chart data
        public List<string> TopIngredients { get; set; } = new List<string>();
        public List<int> IngredientCounts { get; set; } = new List<int>();
        public Dictionary<string, int> IngredientFrequency { get; set; } = new Dictionary<string, int>();
    }
}