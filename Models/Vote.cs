using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace OnlineRecipeSharing.Models
{
    public class Vote
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("vote_id")] 
        public string? vote_id { get; set; }

        [Required]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("recipe_id")]
        public string recipe_id { get; set; }

        [Required]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("user_id")]
        public string user_id { get; set; }

        [Required]
        [RegularExpression("like|dislike", ErrorMessage = "Vote type must be either 'like' or 'dislike'")]
        public string vote_type { get; set; } // 'like' or 'dislike'

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime created_at { get; set; } = DateTime.UtcNow;
    }
}