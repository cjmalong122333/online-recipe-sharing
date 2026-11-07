using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class CommentViewModel
    {
        public string? comment_id { get; set; }

        public string? RecipeId { get; set; }

        public string? user_id { get; set; }

        [Required(ErrorMessage = "The Comment field is required.")]
        public string? Comment { get; set; }

        public string? AuthorUsername { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        public string FormattedDate => CreatedAt.ToString("MMMM dd, yyyy 'at' h:mm tt");
    }
}