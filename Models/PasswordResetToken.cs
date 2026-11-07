using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OnlineRecipeSharing.Models
{
    public class PasswordResetToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("token")]
        public string Token { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("is_used")]
        public bool IsUsed { get; set; }

        [BsonElement("used_at")]
        public DateTime? UsedAt { get; set; }
    }
}
