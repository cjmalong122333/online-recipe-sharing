namespace OnlineRecipeSharing.Models.ViewModels
{
    public class ActivityItemViewModel
    {
        public string Type { get; set; } 
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string RecipeId { get; set; }
        public string RecipeTitle { get; set; }
    }
}
