using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class RecipeViewModel
    {
        public string? recipe_id { get; set; }
        public string? user_id { get; set; }

        [Required(ErrorMessage = "Recipe title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        [Display(Name = "Recipe Title")]
        public string title { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        [Display(Name = "Description")]
        public string description { get; set; }

        [Required(ErrorMessage = "At least one ingredient is required")]
        [Display(Name = "Ingredients")]
        public List<string> ingredients { get; set; } = new List<string>();

        [Required(ErrorMessage = "At least one step is required")]
        [Display(Name = "Steps")]
        public List<string> steps { get; set; } = new List<string>();

        [Display(Name = "Recipe Images")]
        public List<IFormFile>? image_files { get; set; }

        public bool is_active { get; set; } = true;

        public List<string> images { get; set; } = new List<string>();

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public string category { get; set; }

        [Display(Name = "Tags (separate with commas)")]
        public string tags_input { get; set; }

        public List<string> tags { get; set; } = new List<string>();

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Recipe Visibility")]
        public string status { get; set; } = "public";

        // For display only (used in Details view)
        public string? author_username { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public int upvotes { get; set; }
        public int comments_count { get; set; }

        // Additional properties for view logic
        public bool IsOwner { get; set; }

        // Add comments collection
        public List<CommentViewModel> Comments { get; set; } = new List<CommentViewModel>();

        // For adding new comments - make this nullable for recipe creation
        public CommentViewModel? NewComment { get; set; }

        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public string? UserVote { get; set; }

        public List<IngredientDetail> IngredientDetails { get; set; } = new List<IngredientDetail>();
        public List<string> ingredientCategories { get; set; } = new List<string>();
        public List<string> ingredientQuantities { get; set; } = new List<string>();
    }
    public class IngredientDetail
    {
        public string Name { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
    }
}