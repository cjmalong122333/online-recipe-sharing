using Microsoft.AspNetCore.Mvc;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using OnlineRecipeSharing.Data;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Diagnostics;

namespace OnlineRecipeSharing.Controllers
{
    public class RecipeController : Controller
    {
        private readonly MongoDBContext _mongoContext;
        private readonly ILogger<RecipeController> _logger;

        public RecipeController(MongoDBContext mongoContext, ILogger<RecipeController> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        // GET: Recipe/Create
        public async Task<IActionResult> Create()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Please log in to create a recipe.";
                return RedirectToAction("Login", "Account");
            }

            // Fetch ingredients
            var ingredients = await _mongoContext.Ingredients
                .Find(i => i.IsActive)
                .SortBy(i => i.Name)
                .ToListAsync();

            // Group ingredients by category
            var ingredientsByCategory = ingredients
                .GroupBy(i => i.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Get distinct categories
            var ingredientCategories = ingredients
                .Select(i => i.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var viewModel = new RecipeViewModel
            {
                NewComment = null,
                ingredients = new List<string> { "" },
                steps = new List<string> { "" }
            };
            ViewBag.Categories = GetCategories();
            ViewBag.IngredientsByCategory = ingredientsByCategory;
            ViewBag.IngredientCategories = ingredientCategories;

            return View(viewModel);
        }

        // POST: Recipe/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecipeViewModel model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to create a recipe.";
                    return RedirectToAction("Login", "Account");
                }

                // Fetch ingredients for the view in case of validation errors
                var ingredients = await _mongoContext.Ingredients
                    .Find(i => i.IsActive)
                    .SortBy(i => i.Name)
                    .ToListAsync();

                var ingredientsByCategory = ingredients
                    .GroupBy(i => i.Category)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var ingredientCategories = ingredients
                    .Select(i => i.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                ViewBag.IngredientsByCategory = ingredientsByCategory;
                ViewBag.IngredientCategories = ingredientCategories;
                ViewBag.Categories = GetCategories();

                // Clear ModelState errors for NewComment
                ModelState.Remove("NewComment.RecipeId");
                ModelState.Remove("NewComment.Comment");
                ModelState.Remove("NewComment.AuthorUsername");
                ModelState.Remove("NewComment.user_id");
                ModelState.Remove("NewComment.comment_id");

                // Process ingredients from form - CORRECTED VERSION
                var validIngredients = new List<string>();

                // Check if we have ingredient data from the form
                if (model.ingredients != null && model.ingredientQuantities != null)
                {
                    _logger.LogInformation($"Processing {model.ingredients.Count} ingredients from form");

                    for (int i = 0; i < model.ingredients.Count; i++)
                    {
                        var ingredientId = model.ingredients[i];
                        var quantity = model.ingredientQuantities[i];
                        var category = model.ingredientCategories != null && i < model.ingredientCategories.Count
                            ? model.ingredientCategories[i]
                            : "";

                        _logger.LogInformation($"Ingredient {i}: ID={ingredientId}, Qty={quantity}, Category={category}");

                        if (!string.IsNullOrEmpty(ingredientId) &&
                            !string.IsNullOrEmpty(quantity) &&
                            ingredientId != "Select Ingredient")
                        {
                            // Get ingredient name from database using the ID
                            var ingredient = await _mongoContext.Ingredients
                                .Find(ing => ing._id == ingredientId)
                                .FirstOrDefaultAsync();

                            if (ingredient != null)
                            {
                                validIngredients.Add($"{quantity} of {ingredient.Name}");
                                _logger.LogInformation($"Added ingredient: {quantity} of {ingredient.Name}");
                            }
                            else
                            {
                                _logger.LogWarning($"Ingredient not found in database: {ingredientId}");
                                // If ingredient not found, use the ID as fallback (shouldn't happen in normal flow)
                                validIngredients.Add($"{quantity} of Unknown Ingredient");
                            }
                        }
                    }
                }

                // Process steps
                var validSteps = model.steps?
                    .Where(s => !string.IsNullOrEmpty(s?.Trim()))
                    .ToList() ?? new List<string>();

                _logger.LogInformation($"Valid ingredients count: {validIngredients.Count}");
                _logger.LogInformation($"Valid steps count: {validSteps.Count}");

                // Manual validation for ingredients and steps
                bool hasValidIngredients = validIngredients.Any();
                bool hasValidSteps = validSteps.Any();

                if (!hasValidIngredients || !hasValidSteps)
                {
                    if (!hasValidIngredients)
                    {
                        ModelState.AddModelError("", "At least one valid ingredient is required. Please select ingredients from the dropdown.");
                        _logger.LogWarning("No valid ingredients found");
                    }
                    if (!hasValidSteps)
                    {
                        ModelState.AddModelError("", "At least one step is required.");
                        _logger.LogWarning("No valid steps found");
                    }

                    return View(model);
                }

                // If we passed manual validation, check if ModelState is valid for other fields
                if (ModelState.IsValid)
                {
                    // Handle image uploads
                    var imageUrls = new List<string>();
                    if (model.image_files != null && model.image_files.Any())
                    {
                        foreach (var imageFile in model.image_files)
                        {
                            if (imageFile.Length > 0)
                            {
                                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                                var filePath = Path.Combine("wwwroot/images/recipes", fileName);

                                // Ensure directory exists
                                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await imageFile.CopyToAsync(stream);
                                }

                                imageUrls.Add($"/images/recipes/{fileName}");
                            }
                        }
                    }

                    // Process tags
                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(model.tags_input))
                    {
                        tags = model.tags_input.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                    }

                    // Create recipe with recipe_id
                    var recipe = new Recipe
                    {
                        recipe_id = ObjectId.GenerateNewId().ToString(),
                        user_id = userId,
                        title = model.title?.Trim() ?? "",
                        description = model.description?.Trim() ?? "",
                        ingredients = validIngredients,
                        steps = validSteps,
                        images = imageUrls,
                        category = model.category?.Trim() ?? "",
                        tags = tags,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow,
                        upvotes = 0,
                        comments_count = 0,
                        status = model.status?.Trim() ?? "public"
                    };

                    await _mongoContext.Recipes.InsertOneAsync(recipe);

                    TempData["SuccessMessage"] = "Recipe created successfully!";
                    return RedirectToAction("Details", new { id = recipe.recipe_id });
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recipe for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while creating the recipe.";

                // Fetch ingredients again for the view
                var ingredients = await _mongoContext.Ingredients
                    .Find(i => i.IsActive)
                    .SortBy(i => i.Name)
                    .ToListAsync();

                var ingredientsByCategory = ingredients
                    .GroupBy(i => i.Category)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var ingredientCategories = ingredients
                    .Select(i => i.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                ViewBag.Categories = GetCategories();
                ViewBag.IngredientsByCategory = ingredientsByCategory;
                ViewBag.IngredientCategories = ingredientCategories;

                return View(model);
            }
        }

        // GET: Recipe/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return NotFound();
                }

                var currentUserId = HttpContext.Session.GetString("UserId");

                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id && (r.status == "public" || r.user_id == currentUserId))
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    return NotFound();
                }

                // Get author info
                var author = await _mongoContext.Users
                    .Find(u => u._id == recipe.user_id)
                    .FirstOrDefaultAsync();

                var isOwner = currentUserId == recipe.user_id;

                // Get user's current vote
                string userVote = null;
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    var userVoteDoc = await _mongoContext.Votes
                        .Find(v => v.recipe_id == id && v.user_id == currentUserId)
                        .FirstOrDefaultAsync();
                    userVote = userVoteDoc?.vote_type;
                }

                // Get like and dislike counts
                var likesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == id && v.vote_type == "like");

                var dislikesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == id && v.vote_type == "dislike");

                // Get comments for this recipe
                var comments = await _mongoContext.Comments
                    .Find(c => c.recipe_id == id)
                    .SortByDescending(c => c.created_at)
                    .ToListAsync();

                // Get usernames for comments
                var commentViewModels = new List<CommentViewModel>();
                foreach (var comment in comments)
                {
                    var commentAuthor = await _mongoContext.Users
                        .Find(u => u._id == comment.user_id)
                        .FirstOrDefaultAsync();

                    commentViewModels.Add(new CommentViewModel
                    {
                        comment_id = comment.comment_id,
                        RecipeId = comment.recipe_id,
                        Comment = comment.comment,
                        AuthorUsername = commentAuthor?.username ?? "Unknown",
                        CreatedAt = comment.created_at,
                        CanEdit = currentUserId == comment.user_id,
                        CanDelete = currentUserId == comment.user_id || isOwner
                    });
                }

                var viewModel = new RecipeViewModel
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
                    upvotes = (int)likesCount,
                    comments_count = recipe.comments_count,
                    status = recipe.status,
                    author_username = author?.username ?? "Unknown",
                    user_id = recipe.user_id,
                    IsOwner = isOwner,
                    Comments = commentViewModels,
                    NewComment = new CommentViewModel { RecipeId = id },
                    LikesCount = (int)likesCount,
                    DislikesCount = (int)dislikesCount,
                    UserVote = userVote
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipe details for ID: {RecipeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the recipe.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Recipe/Vote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(string id, string voteType)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Please log in to vote on recipes." });
                }

                if (voteType != "like" && voteType != "dislike")
                {
                    return Json(new { success = false, message = "Invalid vote type." });
                }

                _logger.LogInformation("Voting recipe ID: {RecipeId} with type: {VoteType} by User ID: {UserId}", id, voteType, userId);

                // Check if recipe exists
                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found with ID: {RecipeId}", id);
                    return Json(new { success = false, message = "Recipe not found." });
                }

                // Check if user has access to the recipe
                if (recipe.status != "public" && recipe.user_id != userId)
                {
                    _logger.LogWarning("User {UserId} doesn't have access to recipe {RecipeId}", userId, id);
                    return Json(new { success = false, message = "You don't have permission to vote on this recipe." });
                }

                // Check if user already voted
                var existingVote = await _mongoContext.Votes
                    .Find(v => v.recipe_id == id && v.user_id == userId)
                    .FirstOrDefaultAsync();

                if (existingVote != null)
                {
                    if (existingVote.vote_type == voteType)
                    {
                        // Remove vote if clicking the same button (toggle off)
                        await _mongoContext.Votes.DeleteOneAsync(v => v.vote_id == existingVote.vote_id);

                        _logger.LogInformation("Vote removed for recipe {RecipeId} by user {UserId}", id, userId);
                    }
                    else
                    {
                        // Change vote type if clicking different button
                        var update = Builders<Vote>.Update
                            .Set(v => v.vote_type, voteType)
                            .Set(v => v.created_at, DateTime.UtcNow);

                        await _mongoContext.Votes.UpdateOneAsync(
                            v => v.vote_id == existingVote.vote_id,
                            update);

                        _logger.LogInformation("Vote changed to {VoteType} for recipe {RecipeId} by user {UserId}", voteType, id, userId);
                    }
                }
                else
                {
                    // Add new vote with vote_id
                    var vote = new Vote
                    {
                        vote_id = ObjectId.GenerateNewId().ToString(),
                        recipe_id = id,
                        user_id = userId,
                        vote_type = voteType,
                        created_at = DateTime.UtcNow
                    };

                    await _mongoContext.Votes.InsertOneAsync(vote);
                    _logger.LogInformation("Vote added for recipe {RecipeId} with type {VoteType} by user {UserId}", id, voteType, userId);
                }

                // Get updated counts
                var likesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == id && v.vote_type == "like");

                var dislikesCount = await _mongoContext.Votes
                    .CountDocumentsAsync(v => v.recipe_id == id && v.vote_type == "dislike");

                // Get user's current vote status
                var currentUserVote = await _mongoContext.Votes
                    .Find(v => v.recipe_id == id && v.user_id == userId)
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    likes = (int)likesCount,
                    dislikes = (int)dislikesCount,
                    userVote = currentUserVote?.vote_type,
                    message = currentUserVote == null ? "Vote removed!" : $"Recipe {voteType}d!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on recipe ID: {RecipeId}", id);
                return Json(new { success = false, message = "An error occurred while voting." });
            }
        }

        // POST: Recipe/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(string recipeId, string commentText)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to add comments.";
                    return RedirectToAction("Login", "Account");
                }

                if (string.IsNullOrWhiteSpace(commentText))
                {
                    TempData["ErrorMessage"] = "Please enter a valid comment.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                // Verify recipe exists and is accessible
                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == recipeId && (r.status == "public" || r.user_id == userId))
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    TempData["ErrorMessage"] = "Recipe not found or you don't have permission to comment on it.";
                    return RedirectToAction("Index", "Home");
                }

                // Get user info for author username
                var user = await _mongoContext.Users
                    .Find(u => u._id == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                var comment = new Comments
                {
                    comment_id = ObjectId.GenerateNewId().ToString(),
                    recipe_id = recipeId,
                    user_id = userId,
                    comment = commentText.Trim(),
                    author_username = user.username,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                await _mongoContext.Comments.InsertOneAsync(comment);

                // Update recipe comment count
                var update = Builders<Recipe>.Update
                    .Inc(r => r.comments_count, 1)
                    .Set(r => r.updated_at, DateTime.UtcNow);

                await _mongoContext.Recipes.UpdateOneAsync(
                    r => r.recipe_id == recipeId,
                    update);

                TempData["SuccessMessage"] = "Comment added successfully!";
                return RedirectToAction("Details", new { id = recipeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment for recipe ID: {RecipeId}", recipeId);
                TempData["ErrorMessage"] = "An error occurred while adding the comment.";
                return RedirectToAction("Details", new { id = recipeId });
            }
        }

        // POST: Recipe/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(string commentId, string recipeId)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to delete comments.";
                    return RedirectToAction("Login", "Account");
                }

                // Check if user owns the comment or is the recipe owner
                var comment = await _mongoContext.Comments
                    .Find(c => c.comment_id == commentId)
                    .FirstOrDefaultAsync();

                if (comment == null)
                {
                    TempData["ErrorMessage"] = "Comment not found.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == recipeId)
                    .FirstOrDefaultAsync();

                bool isCommentOwner = comment.user_id == userId;
                bool isRecipeOwner = recipe?.user_id == userId;

                if (!isCommentOwner && !isRecipeOwner)
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete this comment.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                // Delete the comment
                var result = await _mongoContext.Comments.DeleteOneAsync(c => c.comment_id == commentId);

                if (result.DeletedCount > 0)
                {
                    // Update recipe comment count
                    var update = Builders<Recipe>.Update
                        .Inc(r => r.comments_count, -1)
                        .Set(r => r.updated_at, DateTime.UtcNow);

                    await _mongoContext.Recipes.UpdateOneAsync(
                        r => r.recipe_id == recipeId,
                        update);

                    TempData["SuccessMessage"] = "Comment deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete comment.";
                }

                return RedirectToAction("Details", new { id = recipeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment ID: {CommentId}", commentId);
                TempData["ErrorMessage"] = "An error occurred while deleting the comment.";
                return RedirectToAction("Details", new { id = recipeId });
            }
        }

        // POST: Recipe/EditComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(string commentId, string recipeId, string commentText)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to edit comments.";
                    return RedirectToAction("Login", "Account");
                }

                if (string.IsNullOrWhiteSpace(commentText))
                {
                    TempData["ErrorMessage"] = "Comment cannot be empty.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                // Verify user owns the comment
                var comment = await _mongoContext.Comments
                    .Find(c => c.comment_id == commentId && c.user_id == userId)
                    .FirstOrDefaultAsync();

                if (comment == null)
                {
                    TempData["ErrorMessage"] = "Comment not found or you don't have permission to edit it.";
                    return RedirectToAction("Details", new { id = recipeId });
                }

                var update = Builders<Comments>.Update
                    .Set(c => c.comment, commentText.Trim())
                    .Set(c => c.updated_at, DateTime.UtcNow);

                var result = await _mongoContext.Comments.UpdateOneAsync(
                    c => c.comment_id == commentId && c.user_id == userId,
                    update);

                if (result.ModifiedCount > 0)
                {
                    TempData["SuccessMessage"] = "Comment updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update comment.";
                }

                return RedirectToAction("Details", new { id = recipeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing comment ID: {CommentId}", commentId);
                TempData["ErrorMessage"] = "An error occurred while editing the comment.";
                return RedirectToAction("Details", new { id = recipeId });
            }
        }
        // Add this method to your RecipeController
        [HttpGet]
        public async Task<IActionResult> GetRecipeDetails(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "Recipe ID is required" });
                }

                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    return Json(new { success = false, message = "Recipe not found" });
                }

                return Json(new
                {
                    success = true,
                    title = recipe.title,
                    images = recipe.images,
                    category = recipe.category,
                    description = recipe.description
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recipe details for ID: {RecipeId}", id);
                return Json(new { success = false, message = "An error occurred while fetching recipe details" });
            }
        }

        // GET: Recipe/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to edit recipes.";
                    return RedirectToAction("Login", "Account");
                }

                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id && r.user_id == userId)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    return NotFound();
                }

                var viewModel = new RecipeViewModel
                {
                    recipe_id = recipe.recipe_id,
                    title = recipe.title,
                    description = recipe.description,
                    ingredients = recipe.ingredients,
                    steps = recipe.steps,
                    images = recipe.images,
                    category = recipe.category,
                    tags = recipe.tags,
                    tags_input = string.Join(", ", recipe.tags),
                    created_at = recipe.created_at,
                    upvotes = recipe.upvotes,
                    comments_count = recipe.comments_count,
                    status = recipe.status
                };

                ViewBag.Categories = GetCategories();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipe edit page for ID: {RecipeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the recipe.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Recipe/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, RecipeViewModel model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to edit recipes.";
                    return RedirectToAction("Login", "Account");
                }

                if (ModelState.IsValid)
                {
                    var existingRecipe = await _mongoContext.Recipes
                        .Find(r => r.recipe_id == id && r.user_id == userId)
                        .FirstOrDefaultAsync();

                    if (existingRecipe == null)
                    {
                        return NotFound();
                    }

                    // Handle new image uploads
                    var imageUrls = new List<string>(existingRecipe.images);
                    if (model.image_files != null && model.image_files.Any())
                    {
                        foreach (var imageFile in model.image_files)
                        {
                            if (imageFile.Length > 0)
                            {
                                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                                var filePath = Path.Combine("wwwroot/images/recipes", fileName);

                                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await imageFile.CopyToAsync(stream);
                                }

                                imageUrls.Add($"/images/recipes/{fileName}");
                            }
                        }
                    }

                    // Process tags
                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(model.tags_input))
                    {
                        tags = model.tags_input.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
                    }

                    // Filter out empty ingredients and steps
                    var validIngredients = model.ingredients
                        .Where(i => !string.IsNullOrEmpty(i?.Trim()))
                        .ToList();

                    var validSteps = model.steps
                        .Where(s => !string.IsNullOrEmpty(s?.Trim()))
                        .ToList();

                    // Ensure we have at least one ingredient and one step
                    if (!validIngredients.Any() || !validSteps.Any())
                    {
                        ModelState.AddModelError("", "At least one ingredient and one step are required.");
                        ViewBag.Categories = GetCategories();
                        return View(model);
                    }

                    var update = Builders<Recipe>.Update
                        .Set(r => r.title, model.title?.Trim())
                        .Set(r => r.description, model.description?.Trim())
                        .Set(r => r.ingredients, validIngredients)
                        .Set(r => r.steps, validSteps)
                        .Set(r => r.images, imageUrls)
                        .Set(r => r.category, model.category?.Trim())
                        .Set(r => r.tags, tags)
                        .Set(r => r.updated_at, DateTime.UtcNow)
                        .Set(r => r.status, model.status?.Trim() ?? "public");

                    var result = await _mongoContext.Recipes.UpdateOneAsync(
                        r => r.recipe_id == id && r.user_id == userId,
                        update);

                    if (result.ModifiedCount > 0)
                    {
                        TempData["SuccessMessage"] = "Recipe updated successfully!";
                        return RedirectToAction("Details", new { id = id });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to update recipe. Please try again.";
                    }
                }

                ViewBag.Categories = GetCategories();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recipe ID: {RecipeId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the recipe.";

                ViewBag.Categories = GetCategories();
                return View(model);
            }
        }

        // POST: Recipe/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to delete recipes.";
                    return RedirectToAction("Index", "Home");
                }

                // Find the recipe to ensure it exists and user owns it
                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id && r.user_id == userId)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    TempData["ErrorMessage"] = "Recipe not found or you don't have permission to delete it.";
                    return RedirectToAction("Index", "Home");
                }

                // Update the recipe status to "deleted" instead of deleting
                var update = Builders<Recipe>.Update
                    .Set(r => r.status, "deleted")
                    .Set(r => r.updated_at, DateTime.UtcNow);

                var result = await _mongoContext.Recipes
                    .UpdateOneAsync(r => r.recipe_id == id && r.user_id == userId, update);

                if (result.ModifiedCount > 0)
                {
                    TempData["SuccessMessage"] = "Recipe deleted successfully!";
                    return RedirectToAction("Profile", "Profile");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete recipe. Please try again.";
                    return RedirectToAction("Details", new { id = id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe ID: {RecipeId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the recipe.";
                return RedirectToAction("Details", new { id = id });
            }
        }

        // GET: Recipe/Delete/{id} - Confirmation page
        public async Task<IActionResult> DeleteConfirmation(string id)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to delete recipes.";
                    return RedirectToAction("Login", "Account");
                }

                var recipe = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == id && r.user_id == userId)
                    .FirstOrDefaultAsync();

                if (recipe == null)
                {
                    TempData["ErrorMessage"] = "Recipe not found or you don't have permission to delete it.";
                    return RedirectToAction("Index", "Home");
                }

                var viewModel = new RecipeViewModel
                {
                    recipe_id = recipe.recipe_id,
                    title = recipe.title,
                    description = recipe.description,
                    images = recipe.images,
                    category = recipe.category,
                    created_at = recipe.created_at
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading delete confirmation for recipe ID: {RecipeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the delete confirmation.";
                return RedirectToAction("Details", new { id = id });
            }
        }

        // GET: Recipe/Search
        public async Task<IActionResult> Search(string query, string category, string tags)
        {
            try
            {
                var filterBuilder = Builders<Recipe>.Filter;
                var filters = new List<FilterDefinition<Recipe>>();

                // Only show public recipes in search
                filters.Add(filterBuilder.Eq(r => r.status, "public"));

                if (!string.IsNullOrEmpty(query))
                {
                    var searchFilter = filterBuilder.Or(
                        filterBuilder.Regex(r => r.title, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                        filterBuilder.Regex(r => r.description, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                        filterBuilder.Regex(r => r.ingredients, new MongoDB.Bson.BsonRegularExpression(query, "i"))
                    );
                    filters.Add(searchFilter);
                }

                if (!string.IsNullOrEmpty(category))
                {
                    filters.Add(filterBuilder.Eq(r => r.category, category));
                }

                if (!string.IsNullOrEmpty(tags))
                {
                    var tagList = tags.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    if (tagList.Any())
                    {
                        // Use regex matching for tags instead of AnyIn
                        var tagFilters = new List<FilterDefinition<Recipe>>();
                        foreach (var tag in tagList)
                        {
                            tagFilters.Add(filterBuilder.Regex(r => r.tags, new MongoDB.Bson.BsonRegularExpression(tag, "i")));
                        }
                        filters.Add(filterBuilder.Or(tagFilters));
                    }
                }

                var combinedFilter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;
                var recipes = await _mongoContext.Recipes
                    .Find(combinedFilter)
                    .ToListAsync();

                // Get author usernames
                var recipeViewModels = new List<RecipeViewModel>();
                foreach (var recipe in recipes)
                {
                    var author = await _mongoContext.Users
                        .Find(u => u._id == recipe.user_id)
                        .FirstOrDefaultAsync();

                    // Get like and dislike counts for search results
                    var likesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                    var dislikesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                    recipeViewModels.Add(new RecipeViewModel
                    {
                        recipe_id = recipe.recipe_id,
                        title = recipe.title,
                        description = recipe.description,
                        images = recipe.images,
                        category = recipe.category,
                        tags = recipe.tags,
                        created_at = recipe.created_at,
                        upvotes = (int)likesCount,
                        LikesCount = (int)likesCount,
                        DislikesCount = (int)dislikesCount,
                        comments_count = recipe.comments_count,
                        author_username = author?.username ?? "Unknown"
                    });
                }

                // Sort by likes count (descending) - Posts with more likes appear first
                recipeViewModels = recipeViewModels
                    .OrderByDescending(r => r.LikesCount)
                    .ThenByDescending(r => r.created_at)
                    .ToList();

                ViewBag.Categories = GetCategories();
                ViewBag.SearchQuery = query;
                ViewBag.SelectedCategory = category;
                ViewBag.SearchTags = tags;

                return View(recipeViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching recipes with query: {Query}", query);
                TempData["ErrorMessage"] = "An error occurred while searching recipes.";
                return View(new List<RecipeViewModel>());
            }
        }

        // Add this method to get ingredients for AJAX requests
        [HttpGet]
        public async Task<IActionResult> GetIngredientsByCategory(string category)
        {
            try
            {
                var ingredients = await _mongoContext.Ingredients
                    .Find(i => i.Category == category && i.IsActive)
                    .SortBy(i => i.Name)
                    .ToListAsync();

                return Json(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ingredients for category: {Category}", category);
                return Json(new List<Ingredient>());
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
            };
        }
    }
}