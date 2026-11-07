using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OnlineRecipeSharing.Data;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using System.Linq;

namespace OnlineRecipeSharing.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly IMongoContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IMongoContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                _logger.LogInformation("Admin Dashboard accessed by user: {User}", User.Identity.Name);

                // Get all active recipes
                var allRecipes = await _context.Recipes
                    .Find(r => r.is_active)
                    .ToListAsync();

                // Get all likes from Vote collection
                var allLikes = await _context.Votes
                    .Find(v => v.vote_type == "like")
                    .ToListAsync();

                // Calculate like counts for each recipe
                var recipesWithLikes = allRecipes.Select(recipe => new
                {
                    Recipe = recipe,
                    LikeCount = allLikes.Count(v => v.recipe_id == recipe.recipe_id)
                }).ToList();

                // Get top recipes by like count
                var topRecipes = recipesWithLikes
                    .OrderByDescending(r => r.LikeCount)
                    .Take(5)
                    .Select(r => r.Recipe)
                    .ToList();

                // Store like counts in ViewData for use in the view
                ViewData["RecipeLikes"] = recipesWithLikes.ToDictionary(
                    x => x.Recipe.recipe_id,
                    x => x.LikeCount
                );

                // Get top users by recipe count
                var allUsers = await _context.Users.Find(u => u.is_active).ToListAsync();
                var topUsers = new List<User>();

                foreach (var user in allUsers)
                {
                    user.RecipeCount = (int)await _context.Recipes
                        .CountDocumentsAsync(r => r.user_id == user._id && r.is_active);
                }

                topUsers = allUsers
                    .Where(u => u.RecipeCount > 0)
                    .OrderByDescending(u => u.RecipeCount)
                    .Take(5)
                    .ToList();

                // Analyze ingredients from all recipes
                var ingredientAnalysis = AnalyzeIngredients(allRecipes);

                // Get top 6 ingredients
                var topIngredients = ingredientAnalysis
                    .OrderByDescending(x => x.Value)
                    .Take(6)
                    .ToList();

                var model = new AdminDashboardViewModel
                {
                    TotalUsers = await _context.Users.CountDocumentsAsync(u => u.is_active),
                    TotalRecipes = await _context.Recipes.CountDocumentsAsync(r => r.is_active),
                    TotalReports = await _context.Reports.CountDocumentsAsync(_ => true),
                    PendingReports = await _context.Reports.CountDocumentsAsync(r => r.status == "pending"),
                    RecentRecipes = await _context.Recipes
                        .Find(r => r.is_active)
                        .SortByDescending(r => r.created_at)
                        .Limit(5)
                        .ToListAsync(),
                    RecentReports = await _context.Reports
                        .Find(r => r.status == "Pending")
                        .SortByDescending(r => r.CreatedAt)
                        .Limit(5)
                        .ToListAsync(),
                    TopRecipes = topRecipes,
                    TopUsers = topUsers,
                    TopIngredients = topIngredients.Select(x => x.Key).ToList(),
                    IngredientCounts = topIngredients.Select(x => x.Value).ToList(),
                    IngredientFrequency = ingredientAnalysis
                };

                _logger.LogInformation("Dashboard data loaded - Users: {Users}, Recipes: {Recipes}, Reports: {Reports}, Pending: {Pending}",
                    model.TotalUsers, model.TotalRecipes, model.TotalReports, model.PendingReports);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["Error"] = "Error loading dashboard";
                return View(new AdminDashboardViewModel());
            }
        }

        // Add these helper methods to your AdminController
        private Dictionary<string, int> AnalyzeIngredients(List<Recipe> recipes)
        {
            var ingredientFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var stopWords = new HashSet<string>
    {
        "of", "and", "or", "the", "a", "an", "to", "in", "on", "at",
        "with", "for", "by", "as", "from", "about", "into", "over", "after",
        "cup", "cups", "tablespoon", "tablespoons", "tbsp", "teaspoon", "teaspoons", "tsp",
        "ounce", "ounces", "oz", "pound", "pounds", "lb", "gram", "grams", "g",
        "kilogram", "kilograms", "kg", "milliliter", "milliliters", "ml", "liter", "liters", "l",
        "piece", "pieces", "slice", "slices", "clove", "cloves", "bunch", "pinch", "dash",
        "to", "taste", "fresh", "dried", "ground", "powdered", "chopped", "sliced", "diced",
        "minced", "grated", "crushed", "whole", "raw", "cooked", "roasted", "boiled"
    };

            foreach (var recipe in recipes)
            {
                if (recipe.ingredients != null && recipe.ingredients.Any())
                {
                    foreach (var ingredient in recipe.ingredients)
                    {
                        if (string.IsNullOrWhiteSpace(ingredient))
                            continue;

                        // Parse ingredient string to extract main ingredient
                        var mainIngredient = ExtractMainIngredient(ingredient, stopWords);

                        if (!string.IsNullOrWhiteSpace(mainIngredient))
                        {
                            // Normalize the ingredient name
                            var normalizedIngredient = mainIngredient.ToLower();

                            if (ingredientFrequency.ContainsKey(normalizedIngredient))
                            {
                                ingredientFrequency[normalizedIngredient]++;
                            }
                            else
                            {
                                ingredientFrequency[normalizedIngredient] = 1;
                            }
                        }
                    }
                }
            }

            // Convert to proper case for display
            var result = new Dictionary<string, int>();
            foreach (var kvp in ingredientFrequency)
            {
                var displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(kvp.Key);
                result[displayName] = kvp.Value;
            }

            return result;
        }

        private string ExtractMainIngredient(string ingredient, HashSet<string> stopWords)
        {
            try
            {
                // Remove quantities and measurements
                ingredient = System.Text.RegularExpressions.Regex.Replace(
                    ingredient,
                    @"^\s*(\d+(\.\d+)?\s*\/\s*\d+\s+|\d+(\.\d+)?\s+|½|¼|¾|⅓|⅔|⅛|\d+\s*(cup|cups|tbsp|tablespoon|tablespoons|tsp|teaspoon|teaspoons|oz|ounce|ounces|lb|pound|pounds|g|gram|grams|kg|kilogram|kilograms|ml|milliliter|milliliters|l|liter|liters|piece|pieces|slice|slices|clove|cloves|bunch|pinch|dash|to taste|fresh|dried|ground|powdered|chopped|sliced|diced|minced|grated|crushed|whole|raw|cooked|roasted|boiled)s?\s*)",
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Split into words and filter
                var words = ingredient.Split(new[] { ' ', ',', ';', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => word.Trim().ToLower())
                    .Where(word => !stopWords.Contains(word))
                    .Where(word => word.Length > 1) // Skip single characters
                    .ToList();

                // Get the first significant word
                var mainWord = words.FirstOrDefault();

                if (!string.IsNullOrEmpty(mainWord))
                {
                    return mainWord;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting ingredient from: {Ingredient}", ingredient);
                return string.Empty;
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid category ID";
                return RedirectToAction("Ingredients");
            }

            try
            {
                // First get the category to find its name
                var category = await _context.Categories.Find(c => c._id == id).FirstOrDefaultAsync();
                if (category == null)
                {
                    TempData["Error"] = "Category not found";
                    return RedirectToAction("Ingredients");
                }

                // Check if category is being used by any ingredients (compare by category name)
                var ingredientsUsingCategory = await _context.Ingredients
                    .CountDocumentsAsync(i => i.Category == category.CategoryName && i.IsActive);

                if (ingredientsUsingCategory > 0)
                {
                    TempData["Error"] = "Cannot delete category that is being used by ingredients";
                    return RedirectToAction("Ingredients");
                }

                // Soft delete category
                var update = Builders<Category>.Update.Set(c => c.IsActive, false);
                var result = await _context.Categories.UpdateOneAsync(c => c._id == id, update);

                if (result.ModifiedCount > 0)
                    TempData["Success"] = "Category deleted successfully";
                else
                    TempData["Error"] = "Category not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category");
                TempData["Error"] = "Error deleting category";
            }

            return RedirectToAction("Ingredients");
        }

        // Get Categories for dropdown
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Find(c => c.IsActive)
                    .SortBy(c => c.CategoryName)
                    .ToListAsync();

                return Json(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
                return Json(new List<Category>());
            }
        }

        // Ingredients Management
        public async Task<IActionResult> Ingredients()
        {
            try
            {
                var ingredients = await _context.Ingredients.Find(i => i.IsActive).ToListAsync();
                var categories = await _context.Categories.Find(c => c.IsActive).ToListAsync();

                var viewModel = new IngredientManagementViewModel
                {
                    Ingredients = ingredients,
                    Categories = categories
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ingredients");
                TempData["Error"] = "Error loading ingredients";
                return View(new IngredientManagementViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIngredient(string name, string category)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
            {
                TempData["Error"] = "Name and category are required";
                return RedirectToAction("Ingredients");
            }

            try
            {
                var ingredient = new Ingredient
                {
                    Name = name.Trim(),
                    Category = category.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.Ingredients.InsertOneAsync(ingredient);
                TempData["Success"] = "Ingredient added successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding ingredient");
                TempData["Error"] = "Error adding ingredient";
            }

            return RedirectToAction("Ingredients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIngredient(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid ingredient ID";
                return RedirectToAction("Ingredients");
            }

            try
            {
                // Soft delete
                var update = Builders<Ingredient>.Update.Set(i => i.IsActive, false);
                var result = await _context.Ingredients.UpdateOneAsync(i => i._id == id, update);

                if (result.ModifiedCount > 0)
                    TempData["Success"] = "Ingredient deleted successfully";
                else
                    TempData["Error"] = "Ingredient not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ingredient");
                TempData["Error"] = "Error deleting ingredient";
            }

            return RedirectToAction("Ingredients");
        }

        // Users Management
        public async Task<IActionResult> Users()
        {
            try
            {
                var users = await _context.Users.Find(_ => true).ToListAsync();

                // Calculate recipe counts for each user
                foreach (var user in users)
                {
                    user.RecipeCount = (int)await _context.Recipes
                        .CountDocumentsAsync(r => r.user_id == user._id && r.is_active);
                }

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                TempData["Error"] = "Error loading users";
                return View(new List<User>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid user ID";
                return RedirectToAction("Users");
            }

            try
            {
                // Soft delete user
                var userUpdate = Builders<User>.Update.Set(u => u.is_active, false);
                var userResult = await _context.Users.UpdateOneAsync(u => u._id == id, userUpdate);

                // Soft delete user's recipes
                var recipeUpdate = Builders<Recipe>.Update.Set(r => r.is_active, false);
                await _context.Recipes.UpdateManyAsync(r => r.user_id == id, recipeUpdate);

                if (userResult.ModifiedCount > 0)
                    TempData["Success"] = "User deleted successfully";
                else
                    TempData["Error"] = "User not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["Error"] = "Error deleting user";
            }

            return RedirectToAction("Users");
        }

        // Reported Posts 
        public async Task<IActionResult> ReportedPosts()
        {
            try
            {
                _logger.LogInformation("Loading reported posts...");

                // Use the exact field names from your database
                var reports = await _context.Reports
                    .Find(r => r.status == "pending") // Now this should match the database field
                    .SortByDescending(r => r.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} pending reports", reports.Count);

                // Convert to ReportViewModel
                var reportViewModels = new List<ReportViewModel>();

                foreach (var report in reports)
                {
                    try
                    {
                        _logger.LogDebug("Processing report {ReportId} with recipe_id: {RecipeId}",
                            report._id, report.RecipeId);

                        // Get recipe title
                        string recipeTitle = "Unknown Recipe";
                        if (!string.IsNullOrEmpty(report.RecipeId))
                        {
                            var recipe = await _context.Recipes
                                .Find(r => r.recipe_id == report.RecipeId)
                                .FirstOrDefaultAsync();
                            recipeTitle = recipe?.title ?? "Deleted Recipe";
                        }

                        // Get reporter username
                        string reporterUsername = "Unknown User";
                        if (!string.IsNullOrEmpty(report.ReportedBy))
                        {
                            var reporter = await _context.Users
                                .Find(u => u._id == report.ReportedBy)
                                .FirstOrDefaultAsync();
                            reporterUsername = reporter?.username ?? "Unknown User";
                        }

                        reportViewModels.Add(new ReportViewModel
                        {
                            Report = report,
                            RecipeTitle = recipeTitle,
                            ReporterUsername = reporterUsername,
                            RecipeId = report.RecipeId
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing report {ReportId}", report._id);
                        // Continue with other reports even if one fails
                        reportViewModels.Add(new ReportViewModel
                        {
                            Report = report,
                            RecipeTitle = "Error Loading Recipe",
                            ReporterUsername = "Error Loading User",
                            RecipeId = report.RecipeId ?? "unknown"
                        });
                    }
                }

                return View(reportViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reported posts");
                TempData["ErrorMessage"] = "Error loading reported posts: " + ex.Message;
                return View(new List<ReportViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string reportId, string status)
        {
            try
            {
                if (string.IsNullOrEmpty(reportId) || string.IsNullOrEmpty(status))
                {
                    return Json(new { success = false, message = "Report ID and status are required." });
                }

                _logger.LogInformation("Updating report {ReportId} to status {Status}", reportId, status);

                var validStatuses = new[] { "reviewed", "dismissed", "resolved", "pending" };
                var lowerStatus = status.ToLower();

                if (!validStatuses.Contains(lowerStatus))
                {
                    return Json(new { success = false, message = "Invalid status." });
                }

                // If dismissing, also remove the recipe
                if (lowerStatus == "dismissed")
                {
                    var report = await _context.Reports.Find(r => r._id == reportId).FirstOrDefaultAsync();
                    if (report != null && !string.IsNullOrEmpty(report.RecipeId))
                    {
                        // Soft delete the recipe
                        var recipeUpdate = Builders<Recipe>.Update.Set(r => r.is_active, false);
                        var recipeResult = await _context.Recipes.UpdateOneAsync(
                            r => r.recipe_id == report.RecipeId,
                            recipeUpdate);

                        _logger.LogInformation("Soft deleted recipe {RecipeId}, modified: {ModifiedCount}",
                            report.RecipeId, recipeResult.ModifiedCount);
                    }
                }

                var update = Builders<Report>.Update
                    .Set(r => r.status, lowerStatus)
                    .Set(r => r.ResolvedAt, lowerStatus != "pending" ? DateTime.UtcNow : (DateTime?)null)
                    .Set(r => r.ResolvedBy, lowerStatus != "pending" ? User.Identity?.Name : null);

                var result = await _context.Reports.UpdateOneAsync(
                    r => r._id == reportId,
                    update);

                if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation("Report {ReportId} status updated to {Status} by user {UserId}",
                        reportId, status, User.Identity.Name);
                    return Json(new { success = true, message = $"Report marked as {status}." });
                }
                else
                {
                    _logger.LogWarning("Report {ReportId} not found or no changes made", reportId);
                    return Json(new { success = false, message = "Report not found or no changes made." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report status for report {ReportId}", reportId);
                return Json(new { success = false, message = "An error occurred while updating the report: " + ex.Message });
            }
        }


        // Get Report Stats 
        [HttpGet]
        public async Task<IActionResult> GetReportStats()
        {
            try
            {
                // Use CountDocumentsAsync for better performance
                var totalReportsTask = _context.Reports.CountDocumentsAsync(FilterDefinition<Report>.Empty);
                var pendingReportsTask = _context.Reports.CountDocumentsAsync(r => r.status == "pending"); // Changed Status to status
                var resolvedReportsTask = _context.Reports.CountDocumentsAsync(r => r.status == "resolved"); // Changed Status to status
                var dismissedReportsTask = _context.Reports.CountDocumentsAsync(r => r.status == "dismissed"); // Changed Status to status

                await Task.WhenAll(totalReportsTask, pendingReportsTask, resolvedReportsTask, dismissedReportsTask);

                var stats = new
                {
                    totalReports = totalReportsTask.Result,
                    pendingReports = pendingReportsTask.Result,
                    resolvedReports = resolvedReportsTask.Result,
                    dismissedReports = dismissedReportsTask.Result
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report statistics");
                return Json(new { success = false, message = "An error occurred while loading statistics." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveReport(string reportId, string action)
        {
            if (string.IsNullOrEmpty(reportId) || string.IsNullOrEmpty(action))
            {
                TempData["Error"] = "Invalid request";
                return RedirectToAction("ReportedPosts");
            }

            try
            {
                var report = await _context.Reports.Find(r => r._id == reportId).FirstOrDefaultAsync();

                if (report == null)
                {
                    TempData["Error"] = "Report not found";
                    return RedirectToAction("ReportedPosts");
                }

                if (action == "remove" && !string.IsNullOrEmpty(report.RecipeId))
                {
                    // Soft delete the reported recipe
                    var recipeUpdate = Builders<Recipe>.Update.Set(r => r.is_active, false);
                    await _context.Recipes.UpdateOneAsync(r => r.recipe_id == report.RecipeId, recipeUpdate);
                    TempData["Success"] = "Report resolved and post removed";
                }
                else if (action == "keep")
                {
                    TempData["Success"] = "Report resolved - post kept";
                }

                // Mark report as resolved
                var update = Builders<Report>.Update
                    .Set(r => r.status, "Resolved")
                    .Set(r => r.ResolvedAt, DateTime.UtcNow)
                    .Set(r => r.ResolvedBy, User.Identity.Name);

                await _context.Reports.UpdateOneAsync(r => r._id == reportId, update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving report");
                TempData["Error"] = "Error resolving report";
            }

            return RedirectToAction("ReportedPosts");
        }

        // Add Category Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["Error"] = "Category name is required";
                return RedirectToAction("Ingredients");
            }

            try
            {
                // Check if category already exists (case insensitive)
                var existingCategory = await _context.Categories
                    .Find(c => c.CategoryName.ToLower() == categoryName.Trim().ToLower() && c.IsActive)
                    .FirstOrDefaultAsync();

                if (existingCategory != null)
                {
                    TempData["Error"] = $"Category '{categoryName}' already exists";
                    return RedirectToAction("Ingredients");
                }

                var category = new Category
                {
                    CategoryName = categoryName.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.Categories.InsertOneAsync(category);
                TempData["Success"] = $"Category '{categoryName}' added successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding category");
                TempData["Error"] = "Error adding category";
            }

            return RedirectToAction("Ingredients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(string FirstName, string LastName, string username, string email, string password, string role = "user")
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) ||
                    string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    TempData["Error"] = "All required fields must be filled out";
                    return RedirectToAction("Users");
                }

                // Check if username already exists
                var existingUsername = await _context.Users
                    .Find(u => u.username.ToLower() == username.ToLower())
                    .FirstOrDefaultAsync();

                if (existingUsername != null)
                {
                    TempData["Error"] = "Username already exists";
                    return RedirectToAction("Users");
                }

                // Check if email already exists
                var existingEmail = await _context.Users
                    .Find(u => u.email.ToLower() == email.ToLower())
                    .FirstOrDefaultAsync();

                if (existingEmail != null)
                {
                    TempData["Error"] = "Email already exists";
                    return RedirectToAction("Users");
                }

                // Hash the password (using the same method as in your ProfileController)
                var hashedPassword = HashPassword(password);

                // Create new user
                var user = new User
                {
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    username = username.Trim(),
                    email = email.Trim().ToLower(),
                    password = hashedPassword,
                    role = role,
                    profile_image = "",
                    date_created = DateTime.UtcNow,
                    last_login = null,
                    is_active = true,
                    RecipeCount = 0
                };

                await _context.Users.InsertOneAsync(user);

                TempData["Success"] = $"User '{username}' created successfully";
                _logger.LogInformation("New user created by admin: {Username}", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new user");
                TempData["Error"] = "Error creating user";
            }

            return RedirectToAction("Users");
        }

        // Add the HashPassword method if it doesn't exist in your AdminController
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        // Add this to your AdminController - Temporary debug action
        [HttpGet]
        public async Task<IActionResult> DebugReports()
        {
            try
            {
                // Get ALL reports regardless of status
                var allReports = await _context.Reports.Find(_ => true).ToListAsync();

                // Log all reports for debugging
                foreach (var report in allReports)
                {
                    _logger.LogInformation("DEBUG - Report ID: {Id}, Status: {Status}, RecipeId: {RecipeId}, Reason: {Reason}",
                        report._id, report.status, report.RecipeId, report.Reason);
                }

                // Check with different status filters
                var pendingLower = await _context.Reports.CountDocumentsAsync(r => r.status.ToLower() == "pending");
                var pendingUpper = await _context.Reports.CountDocumentsAsync(r => r.status == "Pending");
                var anyStatus = await _context.Reports.CountDocumentsAsync(r => !string.IsNullOrEmpty(r.status));

                var debugInfo = new
                {
                    TotalReports = allReports.Count,
                    PendingLowercase = pendingLower,
                    PendingUppercase = pendingUpper,
                    AnyStatus = anyStatus,
                    Reports = allReports.Select(r => new
                    {
                        Id = r._id,
                        Status = r.status,
                        RecipeId = r.RecipeId,
                        Reason = r.Reason,
                        CreatedAt = r.CreatedAt
                    }).ToList()
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DebugReports");
                return Json(new { error = ex.Message });
            }


        }

        // Add this method to your AdminController
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

                // Find the recipe by recipe_id (since that's your field name)
                var recipe = await _context.Recipes
                    .Find(r => r.recipe_id == id && r.is_active)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found with ID: {RecipeId}", id);
                    return Json(new { success = false, message = "Recipe not found" });
                }

                // Get user info for the recipe author
                var author = await _context.Users
                    .Find(u => u._id == recipe.user_id)
                    .FirstOrDefaultAsync();

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
                        upvotes = recipe.upvotes,
                        comments_count = recipe.comments_count
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
    }
}