using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models
{
    public class Recipe
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("recipe_id")] 
        public string? recipe_id { get; set; }

        [Required]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("user_id")]
        public string user_id { get; set; }

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

        [Display(Name = "Images")]
        public List<string> images { get; set; } = new List<string>();

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public string category { get; set; }

        [Display(Name = "Tags")]
        public List<string> tags { get; set; } = new List<string>();

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime updated_at { get; set; } = DateTime.UtcNow;

        public int upvotes { get; set; } = 0;

        public int comments_count { get; set; } = 0;

        public string status { get; set; } = "public";

        public bool is_active { get; set; } = true;
    }
}