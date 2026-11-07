using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class ProfileViewModel
    {
        public string? _id { get; set; }

        // Name fields (read-only)
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Middle Name")]
        public string MiddleName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [Display(Name = "Username")]
        public string username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string email { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters")]
        [Display(Name = "About Me")]
        public string? bio { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePictureFile { get; set; }
        public string? profile_image { get; set; }

        // Statistics (read-only)
        public int RecipesCount { get; set; }
        public string role { get; set; } = string.Empty;
        public DateTime date_created { get; set; }
        public DateTime? last_login { get; set; }
        public bool is_active { get; set; }

        public List<ActivityItemViewModel> RecentActivities { get; set; } = new List<ActivityItemViewModel>();
    }
}