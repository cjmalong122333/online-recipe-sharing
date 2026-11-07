namespace OnlineRecipeSharing.Models
{
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    namespace OnlineRecipeSharing.Models
    {
        public class Category
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string _id { get; set; }

            [BsonElement("categoryname")]
            public string CategoryName { get; set; }

            [BsonElement("created_at")]
            public DateTime CreatedAt { get; set; }

            [BsonElement("is_active")]
            public bool IsActive { get; set; } = true;
        }
    }
}
