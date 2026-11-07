using Microsoft.AspNetCore.Mvc;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using OnlineRecipeSharing.Data;
using MongoDB.Driver;

namespace OnlineRecipeSharing.Controllers
{
    public class ProfileController : Controller
    {
        private readonly MongoDBContext _mongoContext;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(MongoDBContext mongoContext, ILogger<ProfileController> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        // GET: Profile/Profile (Main profile page)
        public async Task<IActionResult> Profile()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to view your profile.";
                    return RedirectToAction("Home", "Home");
                }

                // Get user from Users collection
                var user = await _mongoContext.Users
                    .Find(u => u._id == userId && u.is_active)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found or account is deactivated.";
                    return RedirectToAction("Home", "Home");
                }

                // Update last login time
                var update = Builders<User>.Update.Set(u => u.last_login, DateTime.UtcNow);
                await _mongoContext.Users.UpdateOneAsync(u => u._id == userId, update);

                // Create the combined view model
                var viewModel = new ProfilePageViewModel
                {
                    Profile = new ProfileViewModel
                    {
                        _id = user._id,
                        FirstName = user.FirstName,
                        MiddleName = user.MiddleName,
                        LastName = user.LastName,
                        username = user.username,
                        email = user.email,
                        bio = user.bio,
                        profile_image = user.profile_image,
                        role = user.role,
                        date_created = user.date_created,
                        last_login = user.last_login,
                        is_active = user.is_active
                    },
                    RecentActivities = await GetRecentActivities(userId)
                };

                // Get user's recipes count
                var recipesCount = await _mongoContext.Recipes
                    .CountDocumentsAsync(r => r.user_id == userId && r.status == "public");

                viewModel.Profile.RecipesCount = (int)recipesCount;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Home", "Home");
            }
        }

        // GET: Profile/Favorites
        public IActionResult Favorites()
        {
            try
            {
                // Since favorites are stored in localStorage on the client side,
                // we don't need to query the database here.
                // The view will handle reading from localStorage directly.

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading favorites page");
                TempData["ErrorMessage"] = "An error occurred while loading your favorites.";
                return RedirectToAction("Home", "Home");
            }
        }

        // FIXED: Changed return type from ActivityItem to ActivityItemViewModel and fixed typo
        private async Task<List<ActivityItemViewModel>> GetRecentActivities(string userId)
        {
            var activities = new List<ActivityItemViewModel>();

            try
            {
                // Get recent recipes created by user
                var recentRecipes = await _mongoContext.Recipes
                    .Find(r => r.user_id == userId)
                    .SortByDescending(r => r.created_at)
                    .Limit(5)
                    .ToListAsync();

                foreach (var recipe in recentRecipes)
                {
                    activities.Add(new ActivityItemViewModel
                    {
                        Type = "recipe_created",
                        Description = $"Created recipe: {recipe.title}",
                        Timestamp = recipe.created_at,
                        RecipeId = recipe.recipe_id,
                        RecipeTitle = recipe.title
                    });
                }

                // Get recent comments by user
                var recentComments = await _mongoContext.Comments
                    .Find(c => c.user_id == userId)
                    .SortByDescending(c => c.created_at)
                    .Limit(5)
                    .ToListAsync();

                foreach (var comment in recentComments)
                {
                    var recipe = await _mongoContext.Recipes
                        .Find(r => r.recipe_id == comment.recipe_id)
                        .FirstOrDefaultAsync();

                    activities.Add(new ActivityItemViewModel
                    {
                        Type = "comment_added",
                        Description = $"Commented on: {recipe?.title ?? "a recipe"}",
                        Timestamp = comment.created_at,
                        RecipeId = comment.recipe_id,
                        RecipeTitle = recipe?.title
                    });
                }

                // Get recent likes by user
                var recentLikes = await _mongoContext.Votes
                    .Find(v => v.user_id == userId && v.vote_type == "like")
                    .SortByDescending(v => v.created_at)
                    .Limit(5)
                    .ToListAsync();

                foreach (var like in recentLikes)
                {
                    var recipe = await _mongoContext.Recipes
                        .Find(r => r.recipe_id == like.recipe_id)
                        .FirstOrDefaultAsync();

                    activities.Add(new ActivityItemViewModel // FIXED: Changed from ActivityItemV to ActivityItemViewModel
                    {
                        Type = "liked_recipe",
                        Description = $"Liked recipe: {recipe?.title ?? "a recipe"}",
                        Timestamp = like.created_at,
                        RecipeId = like.recipe_id,
                        RecipeTitle = recipe?.title
                    });
                }

                // Sort all activities by timestamp and take the most recent 10
                activities = activities
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .ToList();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent activities for user: {UserId}", userId);
            }

            return activities;
        }

        // GET: Profile/Edit
        public async Task<IActionResult> Edit()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to edit your profile.";
                    return RedirectToAction("Home", "Home");
                }

                var user = await _mongoContext.Users
                    .Find(u => u._id == userId && u.is_active)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found or account is deactivated.";
                    return RedirectToAction("Home", "Home");
                }

                var viewModel = new ProfileViewModel
                {
                    _id = user._id,
                    FirstName = user.FirstName,
                    MiddleName = user.MiddleName,
                    LastName = user.LastName,
                    username = user.username,
                    email = user.email,
                    bio = user.bio,
                    profile_image = user.profile_image,
                    role = user.role,
                    date_created = user.date_created,
                    last_login = user.last_login,
                    is_active = user.is_active
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile edit page for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while loading the edit page.";
                return RedirectToAction("Profile");
            }
        }

        // POST: Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileViewModel model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to edit your profile.";
                    return RedirectToAction("Home", "Home");
                }

                if (ModelState.IsValid)
                {
                    // Check if username is already taken by another user
                    var existingUsername = await _mongoContext.Users
                        .Find(u => u.username == model.username && u._id != userId)
                        .FirstOrDefaultAsync();

                    if (existingUsername != null)
                    {
                        ModelState.AddModelError("username", "Username is already taken.");
                        return View(model);
                    }

                    // Check if email is already taken by another user
                    var existingEmail = await _mongoContext.Users
                        .Find(u => u.email == model.email && u._id != userId)
                        .FirstOrDefaultAsync();

                    if (existingEmail != null)
                    {
                        ModelState.AddModelError("email", "Email is already registered.");
                        return View(model);
                    }

                    // Handle profile picture upload
                    string profileImageUrl = model.profile_image;

                    // FIXED: Added proper null check and file upload handling
                    if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                        var fileExtension = Path.GetExtension(model.ProfilePictureFile.FileName).ToLowerInvariant();

                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError("ProfilePictureFile", "Only image files (JPG, JPEG, PNG, GIF) are allowed.");
                            return View(model);
                        }

                        // Validate file size (max 5MB)
                        if (model.ProfilePictureFile.Length > 5 * 1024 * 1024)
                        {
                            ModelState.AddModelError("ProfilePictureFile", "Profile picture must be less than 5MB.");
                            return View(model);
                        }

                        var fileName = Guid.NewGuid().ToString() + fileExtension;
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");

                        // Ensure directory exists
                        Directory.CreateDirectory(uploadsFolder);

                        var filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ProfilePictureFile.CopyToAsync(stream);
                        }

                        profileImageUrl = $"/images/profiles/{fileName}";
                    }
                    else
                    {
                        // Keep existing profile image if no new file is uploaded
                        profileImageUrl = model.profile_image;
                    }

                    // Create update definition for User document - ADDED missing name fields
                    var update = Builders<User>.Update
                        .Set(u => u.FirstName, model.FirstName)
                        .Set(u => u.MiddleName, model.MiddleName)
                        .Set(u => u.LastName, model.LastName)
                        .Set(u => u.username, model.username)
                        .Set(u => u.email, model.email)
                        .Set(u => u.bio, model.bio ?? "")
                        .Set(u => u.profile_image, profileImageUrl)
                        .Set(u => u.last_login, DateTime.UtcNow);

                    var result = await _mongoContext.Users.UpdateOneAsync(
                        u => u._id == userId && u.is_active,
                        update);

                    if (result.ModifiedCount > 0)
                    {
                        // Update session username if changed
                        if (HttpContext.Session.GetString("Username") != model.username)
                        {
                            HttpContext.Session.SetString("Username", model.username);
                        }

                        // Update session profile image if changed
                        if (!string.IsNullOrEmpty(profileImageUrl))
                        {
                            HttpContext.Session.SetString("ProfileImage", profileImageUrl);
                        }

                        TempData["SuccessMessage"] = "Profile updated successfully!";
                        return RedirectToAction("Profile");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to update profile. Please try again.";
                    }
                }

                // If we got this far, something failed; redisplay form
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while updating your profile.";
                return View(model);
            }
        }

        // GET: Profile/View/{username}
        public async Task<IActionResult> View(string username)
        {
            try
            {
                var user = await _mongoContext.Users
                    .Find(u => u.username == username && u.is_active)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound();
                }

                // Get user's recipes
                var recipes = await _mongoContext.Recipes
                    .Find(r => r.user_id == user._id && r.status == "public")
                    .SortByDescending(r => r.created_at)
                    .Limit(6)
                    .ToListAsync();

                // Create view model
                var viewModel = new ProfileViewModel
                {
                    _id = user._id,
                    FirstName = user.FirstName,
                    MiddleName = user.MiddleName,
                    LastName = user.LastName,
                    username = user.username,
                    email = user.email,
                    bio = user.bio,
                    profile_image = user.profile_image,
                    role = user.role,
                    date_created = user.date_created,
                    last_login = user.last_login,
                    is_active = user.is_active
                };

                // Get recipes count
                viewModel.RecipesCount = (int)await _mongoContext.Recipes.CountDocumentsAsync(r => r.user_id == user._id && r.status == "public");

                ViewBag.UserRecipes = recipes;
                ViewBag.IsOwnProfile = HttpContext.Session.GetString("UserId") == user._id;

                return View("PublicProfile", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing public profile for username: {Username}", username);
                TempData["ErrorMessage"] = "An error occurred while loading the profile.";
                return RedirectToAction("Home", "Home");
            }
        }

        // POST: Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to change your password.";
                    return RedirectToAction("Home", "Home");
                }

                // Basic validation
                if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
                {
                    TempData["ErrorMessage"] = "All password fields are required.";
                    return RedirectToAction("Profile");
                }

                if (NewPassword != ConfirmPassword)
                {
                    TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                    return RedirectToAction("Profile");
                }

                if (NewPassword.Length < 6)
                {
                    TempData["ErrorMessage"] = "New password must be at least 6 characters long.";
                    return RedirectToAction("Profile");
                }

                // Get user and verify current password
                var user = await _mongoContext.Users
                    .Find(u => u._id == userId && u.is_active)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found or account is deactivated.";
                    return RedirectToAction("Profile");
                }

                // Verify current password
                var hashedCurrentPassword = HashPassword(CurrentPassword);
                if (user.password != hashedCurrentPassword)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return RedirectToAction("Profile");
                }

                // Update password
                var hashedNewPassword = HashPassword(NewPassword);
                var update = Builders<User>.Update
                    .Set(u => u.password, hashedNewPassword)
                    .Set(u => u.last_login, DateTime.UtcNow);

                var result = await _mongoContext.Users.UpdateOneAsync(
                    u => u._id == userId && u.is_active,
                    update);

                if (result.ModifiedCount > 0)
                {
                    TempData["SuccessMessage"] = "Password changed successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to change password. Please try again.";
                }

                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while changing your password.";
                return RedirectToAction("Profile");
            }
        }

        // Helper method to hash passwords
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }


        // POST: Profile/Deactivate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Please log in to deactivate your account.";
                    return RedirectToAction("Home", "Home");
                }

                var update = Builders<User>.Update
                    .Set(u => u.is_active, false)
                    .Set(u => u.last_login, DateTime.UtcNow);

                var result = await _mongoContext.Users.UpdateOneAsync(
                    u => u._id == userId,
                    update);

                if (result.ModifiedCount > 0)
                {
                    HttpContext.Session.Clear();
                    TempData["SuccessMessage"] = "Your account has been deactivated successfully.";
                    return RedirectToAction("Home", "Home");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to deactivate account. Please try again.";
                    return RedirectToAction("Profile");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating account for user ID: {UserId}", HttpContext.Session.GetString("UserId"));
                TempData["ErrorMessage"] = "An error occurred while deactivating your account.";
                return RedirectToAction("Profile");
            }
        }
    }
}