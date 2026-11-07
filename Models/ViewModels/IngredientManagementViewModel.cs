using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class IngredientManagementViewModel
    {
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
        public List<Category> Categories { get; set; } = new List<Category>();
    }
}