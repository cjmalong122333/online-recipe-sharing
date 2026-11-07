using System.ComponentModel.DataAnnotations;
using OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Models.ViewModels
{
    public class RecipeFilterViewModel
    {
        public List<RecipeViewModel> Recipes { get; set; } = new List<RecipeViewModel>();
        public List<string> Categories { get; set; } = new List<string>();
        public string SelectedCategory { get; set; }
        public string SelectedTags { get; set; }


        // For dropdown options
        public List<string> GetCategories { get; set; } = new List<string>
        {
            "Appetizer",
            "Main Dish",
            "Dessert",
            "Snack",
            "Breakfast",
            "Lunch",
            "Dinner",
            "Beverage",
            "Soup",
            "Salad",
            "Side Dish",
            "Bread",
            "Cocktail",
            "Sauce",
            "Marinade",
            "Other"
        };
    }
}