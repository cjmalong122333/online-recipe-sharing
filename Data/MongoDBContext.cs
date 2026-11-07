using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Data
{
    public class MongoDBContext : IMongoContext  
    {
        private readonly IMongoDatabase _database;

        public MongoDBContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var mongoClient = new MongoClient(connectionString);

            var databaseName = configuration["MongoDBSettings:DatabaseName"] ?? "RecipeSharingDB";
            _database = mongoClient.GetDatabase(databaseName);
        }

        // Collections (match your IMongoContext interface)
        public IMongoCollection<PasswordResetToken> PasswordResetTokens =>
        _database.GetCollection<PasswordResetToken>("password_reset_tokens");
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Recipe> Recipes => _database.GetCollection<Recipe>("Recipes");
        public IMongoCollection<Report> Reports => _database.GetCollection<Report>("Reports");
        public IMongoCollection<Ingredient> Ingredients => _database.GetCollection<Ingredient>("Ingredients");

        public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");

        public IMongoCollection<Comments> Comments => _database.GetCollection<Comments>("Comments");
        public IMongoCollection<Vote> Votes => _database.GetCollection<Vote>("Votes");
    }
}
