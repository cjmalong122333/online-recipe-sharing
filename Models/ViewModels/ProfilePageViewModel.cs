namespace OnlineRecipeSharing.Models.ViewModels
{
    public class ProfilePageViewModel
    {
        public ProfileViewModel Profile { get; set; }
        public List<ActivityItemViewModel> RecentActivities { get; set; }
    }

    public class Activity
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string RecipeId { get; set; }
    }
}
