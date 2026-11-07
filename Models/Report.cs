using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OnlineRecipeSharing.Models
{
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    namespace OnlineRecipeSharing.Models
    {
        public class Report
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string _id { get; set; }

            [BsonElement("recipe_id")]
            [BsonRepresentation(BsonType.ObjectId)]
            public string RecipeId { get; set; }

            [BsonElement("reported_by")]
            [BsonRepresentation(BsonType.ObjectId)]
            public string ReportedBy { get; set; }

            [BsonElement("reason")]
            public string Reason { get; set; }

            [BsonElement("description")]
            public string description { get; set; }

            [BsonElement("status")]
            public string status { get; set; } = "pending";

            [BsonElement("created_at")]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            [BsonElement("resolved_at")]
            public DateTime? ResolvedAt { get; set; }

            [BsonElement("resolved_by")]
            public string ResolvedBy { get; set; }


            // Navigation properties (not stored in DB)
            [BsonIgnore]
            public string? RecipeTitle { get; set; }

            [BsonIgnore]
            public string? ReporterUsername { get; set; }
        }
    }
}