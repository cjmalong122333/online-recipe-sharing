using Microsoft.AspNetCore.Mvc;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using OnlineRecipeSharing.Data;
using MongoDB.Driver;

namespace OnlineRecipeSharing.Controllers
{
    public class HomeController : Controller
    {
        private readonly MongoDBContext _mongoContext;
        private readonly ILogger<HomeController> _logger;

        public HomeController(MongoDBContext mongoContext, ILogger<HomeController> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        // GET: Home/About
        public IActionResult About()
        {
            return View();
        }

        public async Task<IActionResult> Home()
        {
            try
            {
                // Get featured recipes from MongoDB (only public AND active recipes)
                var featuredRecipes = await _mongoContext.Recipes
                    .Find(r => r.status == "public" && r.is_active == true)
                    .ToListAsync();

                // Initialize dictionary
                var dislikesCounts = new Dictionary<string, int>();

                // Calculate actual like and dislike counts from Votes collection
                foreach (var recipe in featuredRecipes)
                {
                    // Make sure recipe_id is not null
                    if (!string.IsNullOrEmpty(recipe.recipe_id))
                    {
                        var likesCount = await _mongoContext.Votes
                            .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                        var dislikesCount = await _mongoContext.Votes
                            .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                        recipe.upvotes = (int)likesCount; // Keep for backward compatibility

                        // Use TryAdd to avoid duplicate keys and ensure the key exists
                        dislikesCounts.TryAdd(recipe.recipe_id, (int)dislikesCount);
                    }
                }

                ViewBag.DislikesCounts = dislikesCounts;

                // Sort by likes count (descending) and take top 6
                featuredRecipes = featuredRecipes
                    .Where(r => !string.IsNullOrEmpty(r.recipe_id))
                    .OrderByDescending(r => r.upvotes)
                    .ThenByDescending(r => r.created_at)
                    .Take(6)
                    .ToList();

                // If no recipes in database, use mock data
                if (!featuredRecipes.Any())
                {
                    _logger.LogInformation("No active recipes found in database, using mock data");
                    featuredRecipes = GetMockRecipes();
                    // Initialize dislikesCounts for mock recipes too
                    foreach (var recipe in featuredRecipes)
                    {
                        if (!string.IsNullOrEmpty(recipe.recipe_id))
                        {
                            dislikesCounts.TryAdd(recipe.recipe_id, 0); // Default to 0 for mock recipes
                        }
                    }
                    ViewBag.DislikesCounts = dislikesCounts;
                }
                else
                {
                    // Get author usernames for the recipes
                    foreach (var recipe in featuredRecipes)
                    {
                        if (!string.IsNullOrEmpty(recipe.user_id))
                        {
                            var author = await _mongoContext.Users
                                .Find(u => u._id == recipe.user_id)
                                .FirstOrDefaultAsync();

                            // You can add author info to ViewBag or create a view model if needed
                            ViewBag.AuthorNames ??= new Dictionary<string, string>();
                            if (author != null)
                            {
                                ViewBag.AuthorNames[recipe.recipe_id] = author.username;
                            }
                        }
                    }
                }

                return View(featuredRecipes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading featured recipes");
                // Fallback to mock data if database is unavailable
                var mockRecipes = GetMockRecipes();
                var dislikesCounts = new Dictionary<string, int>();
                foreach (var recipe in mockRecipes)
                {
                    if (!string.IsNullOrEmpty(recipe.recipe_id))
                    {
                        dislikesCounts.TryAdd(recipe.recipe_id, 0);
                    }
                }
                ViewBag.DislikesCounts = dislikesCounts;
                return View(mockRecipes);
            }
        }

        // GET: Home/Recipe - Display all recipes with filtering
        public async Task<IActionResult> PublicRecipe(string category, string tags)
        {
            try
            {
                // Build filter for public AND active recipes
                var filterBuilder = Builders<Recipe>.Filter;
                var filter = filterBuilder.Eq(r => r.status, "public") &
                            filterBuilder.Eq(r => r.is_active, true);

                // Apply category filter if provided
                if (!string.IsNullOrEmpty(category))
                {
                    filter &= filterBuilder.Eq(r => r.category, category);
                }

                // Apply tags filter if provided
                if (!string.IsNullOrEmpty(tags))
                {
                    var tagList = tags.Split(',')
                        .Select(t => t.Trim().ToLower())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    if (tagList.Any())
                    {
                        filter &= filterBuilder.AnyIn(r => r.tags, tagList);
                    }
                }

                // Get recipes from MongoDB
                var recipes = await _mongoContext.Recipes
                    .Find(filter)
                    .ToListAsync();

                // Create view models with like and dislike counts
                var recipeViewModels = new List<RecipeViewModel>();
                foreach (var recipe in recipes)
                {
                    // Get like and dislike counts
                    var likesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                    var dislikesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                    // Get author username
                    var author = await _mongoContext.Users
                        .Find(u => u._id == recipe.user_id)
                        .FirstOrDefaultAsync();

                    recipeViewModels.Add(new RecipeViewModel
                    {
                        recipe_id = recipe.recipe_id,
                        title = recipe.title,
                        description = recipe.description,
                        ingredients = recipe.ingredients,
                        steps = recipe.steps,
                        images = recipe.images,
                        category = recipe.category,
                        tags = recipe.tags,
                        created_at = recipe.created_at,
                        updated_at = recipe.updated_at,
                        upvotes = (int)likesCount, // Keep for backward compatibility
                        comments_count = recipe.comments_count,
                        status = recipe.status,
                        is_active = recipe.is_active,
                        author_username = author?.username ?? "Unknown",
                        user_id = recipe.user_id,
                        LikesCount = (int)likesCount,
                        DislikesCount = (int)dislikesCount
                    });
                }

                // Sort by likes count (descending) - Posts with more likes appear first
                recipeViewModels = recipeViewModels
                    .OrderByDescending(r => r.LikesCount)
                    .ThenByDescending(r => r.created_at) // Secondary sort by creation date
                    .ToList();

                // Create view model
                var viewModel = new RecipeFilterViewModel
                {
                    Recipes = recipeViewModels,
                    SelectedCategory = category ?? "",
                    SelectedTags = tags ?? "",
                    Categories = GetCategories()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipes with filters");
                TempData["ErrorMessage"] = "An error occurred while loading recipes.";

                // Return empty view model on error
                return View(new RecipeFilterViewModel { Categories = GetCategories() });
            }
        }

        // Add this method to get recipe details for the admin modal
        [HttpGet]
        public async Task<IActionResult> GetRecipeDetails(string id)
        {
            try
            {
                _logger.LogInformation("Fetching recipe details for ID: {RecipeId}", id);

                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "Recipe ID is required" });
                }

                // Find the recipe by recipe_id (include both active and inactive for admin review)
                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found with ID: {RecipeId}", id);
                    return Json(new { success = false, message = "Recipe not found" });
                }

                // Get user info for the recipe author
                var author = await _mongoContext.Users
                    .Find(u => u._id == recipe.user_id)
                    .FirstOrDefaultAsync();

                // Get actual like and dislike counts
                var likesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                var dislikesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                // Prepare response data
                var recipeData = new
                {
                    success = true,
                    data = new
                    {
                        id = recipe.recipe_id,
                        title = recipe.title,
                        description = recipe.description,
                        ingredients = recipe.ingredients ?? new List<string>(),
                        instructions = recipe.steps ?? new List<string>(),
                        images = recipe.images ?? new List<string>(),
                        category = recipe.category,
                        tags = recipe.tags ?? new List<string>(),
                        prep_time = "N/A", // Add these fields if they exist in your model
                        cook_time = "N/A",
                        servings = "N/A",
                        difficulty = "N/A",
                        author = author?.username ?? "Unknown",
                        created_at = recipe.created_at.ToString("MMMM dd, yyyy"),
                        upvotes = (int)likesCount,
                        dislikes = (int)dislikesCount,
                        comments_count = recipe.comments_count,
                        status = recipe.status,
                        is_active = recipe.is_active,
                        user_id = recipe.user_id
                    }
                };

                _logger.LogInformation("Successfully fetched recipe details for: {RecipeTitle}", recipe.title);
                return Json(recipeData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recipe details for ID: {RecipeId}", id);
                return Json(new { success = false, message = "Error loading recipe details" });
            }
        }

        // Add this method to debug votes
        [HttpGet]
        public async Task<IActionResult> DebugVotes()
        {
            try
            {
                var recipes = await _mongoContext.Recipes
                    .Find(r => r.status == "public" && r.is_active == true)
                    .Limit(5)
                    .ToListAsync();

                var debugInfo = new List<object>();

                foreach (var recipe in recipes)
                {
                    var likesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                    var dislikesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                    debugInfo.Add(new
                    {
                        RecipeId = recipe.recipe_id,
                        RecipeTitle = recipe.title,
                        IsActive = recipe.is_active,
                        Status = recipe.status,
                        StoredUpvotes = recipe.upvotes,
                        ActualLikes = likesCount,
                        ActualDislikes = dislikesCount,
                        HasVotesCollection = _mongoContext.Votes != null
                    });
                }

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Add this method to debug active recipes
        [HttpGet]
        public async Task<IActionResult> DebugActiveRecipes()
        {
            try
            {
                var activeRecipes = await _mongoContext.Recipes
                    .Find(r => r.is_active == true)
                    .ToListAsync();

                var inactiveRecipes = await _mongoContext.Recipes
                    .Find(r => r.is_active == false)
                    .ToListAsync();

                var debugInfo = new
                {
                    TotalActiveRecipes = activeRecipes.Count,
                    TotalInactiveRecipes = inactiveRecipes.Count,
                    ActiveRecipes = activeRecipes.Select(r => new
                    {
                        Id = r.recipe_id,
                        Title = r.title,
                        Status = r.status,
                        IsActive = r.is_active,
                        CreatedAt = r.created_at
                    }),
                    InactiveRecipes = inactiveRecipes.Select(r => new
                    {
                        Id = r.recipe_id,
                        Title = r.title,
                        Status = r.status,
                        IsActive = r.is_active,
                        CreatedAt = r.created_at
                    })
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private List<string> GetCategories()
        {
            return new List<string>
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

        private List<Recipe> GetMockRecipes()
        {
            return new List<Recipe>
            {
                new Recipe
                {
                    recipe_id = "1",
                    title = "Spaghetti Carbonara",
                    description = "A classic Italian pasta dish with eggs, cheese, and pancetta.",
                    ingredients = new List<string>
                    {
                        "400g spaghetti",
                        "200g pancetta",
                        "4 large eggs",
                        "100g Parmesan cheese",
                        "Black pepper",
                        "Salt"
                    },
                    steps = new List<string>
                    {
                        "Cook spaghetti in salted boiling water according to package instructions.",
                        "While pasta cooks, fry pancetta until crispy.",
                        "Whisk eggs and grated Parmesan together in a mixing bowl.",
                        "Drain pasta, reserving some pasta water.",
                        "Combine hot pasta with pancetta, then quickly mix in egg and cheese mixture.",
                        "Add pasta water as needed to create a creamy sauce.",
                        "Season with black pepper and serve immediately."
                    },
                    images = new List<string> { "/images/recipe1.jpg" },
                    category = "Main Dish",
                    tags = new List<string> { "italian", "pasta", "quick", "dinner" },
                    created_at = DateTime.UtcNow.AddDays(-5),
                    updated_at = DateTime.UtcNow.AddDays(-5),
                    upvotes = 24,
                    comments_count = 8,
                    status = "public",
                    is_active = true,
                    user_id = "mock_user_1"
                },
                new Recipe
                {
                    recipe_id = "2",
                    title = "Chicken Curry",
                    description = "Spicy and flavorful Indian chicken curry with aromatic spices.",
                    ingredients = new List<string>
                    {
                        "500g chicken breast",
                        "2 onions, finely chopped",
                        "3 tomatoes, pureed",
                        "2 tbsp curry powder",
                        "1 cup coconut milk",
                        "2 tbsp oil",
                        "Salt to taste"
                    },
                    steps = new List<string>
                    {
                        "Heat oil in a large pan and sauté onions until golden brown.",
                        "Add chicken pieces and cook until they turn white.",
                        "Add curry powder and cook for 2 minutes until fragrant.",
                        "Pour in tomato puree and cook until oil separates.",
                        "Add coconut milk and simmer for 15-20 minutes.",
                        "Season with salt and serve with rice or naan."
                    },
                    images = new List<string> { "/images/recipe2.jpg" },
                    category = "Main Dish",
                    tags = new List<string> { "indian", "spicy", "chicken", "curry" },
                    created_at = DateTime.UtcNow.AddDays(-3),
                    updated_at = DateTime.UtcNow.AddDays(-3),
                    upvotes = 18,
                    comments_count = 12,
                    status = "public",
                    is_active = true,
                    user_id = "mock_user_2"
                },
                new Recipe
                {
                    recipe_id = "3",
                    title = "Greek Salad",
                    description = "Fresh Mediterranean salad with feta cheese and olives.",
                    ingredients = new List<string>
                    {
                        "4 large tomatoes",
                        "1 cucumber",
                        "1 red onion",
                        "200g feta cheese",
                        "100g Kalamata olives",
                        "3 tbsp olive oil",
                        "1 tbsp oregano",
                        "Salt and pepper"
                    },
                    steps = new List<string>
                    {
                        "Chop tomatoes, cucumber, and red onion into bite-sized pieces.",
                        "Combine all vegetables in a large bowl.",
                        "Add olives and crumbled feta cheese.",
                        "Drizzle with olive oil and sprinkle with oregano.",
                        "Season with salt and pepper to taste.",
                        "Toss gently and serve immediately."
                    },
                    images = new List<string> { "/images/recipe3.jpg" },
                    category = "Salad",
                    tags = new List<string> { "greek", "vegetarian", "healthy", "quick" },
                    created_at = DateTime.UtcNow.AddDays(-1),
                    updated_at = DateTime.UtcNow.AddDays(-1),
                    upvotes = 32,
                    comments_count = 5,
                    status = "public",
                    is_active = true,
                    user_id = "mock_user_3"
                }
            };
        }
    }
}