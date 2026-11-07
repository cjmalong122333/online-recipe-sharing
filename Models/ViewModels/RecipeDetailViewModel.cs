using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class RecipeDetailViewModel
    {
        public string? _id { get; set; }

        [Display(Name = "Recipe Title")]
        public string title { get; set; }

        [Display(Name = "Description")]
        public string description { get; set; }

        [Display(Name = "Ingredients")]
        public List<string> ingredients { get; set; } = new List<string>();

        [Display(Name = "Steps")]
        public List<string> steps { get; set; } = new List<string>();

        [Display(Name = "Images")]
        public List<string> images { get; set; } = new List<string>();

        [Display(Name = "Category")]
        public string category { get; set; }

        [Display(Name = "Tags")]
        public List<string> tags { get; set; } = new List<string>();

        [Display(Name = "Author")]
        public string author_username { get; set; }

        [Display(Name = "Created Date")]
        public DateTime created_at { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime updated_at { get; set; }

        [Display(Name = "Upvotes")]
        public int upvotes { get; set; }

        [Display(Name = "Comments")]
        public int comments_count { get; set; }

        [Display(Name = "Status")]
        public string status { get; set; }

        // Additional properties for Details view
        public bool IsOwner { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}