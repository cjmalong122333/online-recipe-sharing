using MongoDB.Driver;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Data
{
    public interface IMongoContext
    {
        IMongoCollection<User> Users { get; }
        IMongoCollection<Recipe> Recipes { get; }
        IMongoCollection<Report> Reports { get; }
        IMongoCollection<Ingredient> Ingredients { get; }
        IMongoCollection<Category> Categories { get; }
        IMongoCollection<Vote> Votes { get; }



    }
}