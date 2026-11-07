using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models
{
    public class Comments
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("comment_id")] 
        public string comment_id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required(ErrorMessage = "The RecipeId field is required.")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("recipe_id")]
        public string recipe_id { get; set; }

        [Required(ErrorMessage = "The user_id field is required.")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("user_id")]
        public string user_id { get; set; }

        [Required(ErrorMessage = "The Comment field is required.")]
        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string comment { get; set; }

        [Required(ErrorMessage = "The AuthorUsername field is required.")]
        public string author_username { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }
}