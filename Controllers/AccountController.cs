using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OnlineRecipeSharing.Data;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using OnlineRecipeSharing.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OnlineRecipeSharing.Controllers
{
    public class AccountController : Controller
    {
        private readonly MongoDBContext _mongoContext;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _emailService;

        public AccountController(MongoDBContext mongoContext,
            ILogger<AccountController> logger,
            IEmailService emailService)
        {
            _mongoContext = mongoContext;
            _logger = logger;
            _emailService = emailService;
        }

        // GET: Login page 
        [HttpGet]
        public IActionResult Login()
        {
            return View("_LoginModal");
        }
        public IActionResult LoginModal()
        {
            return PartialView("~/Views/Shared/_LoginModal.cshtml");
        }

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Please enter both email and password.");
                return View("_LoginModal");
            }

            try
            {
                var hashedPassword = HashPassword(password);
                var user = await _mongoContext.Users
                    .Find(u => u.email == email && u.password == hashedPassword && u.is_active)
                    .FirstOrDefaultAsync();

                if (user != null)
                {
                    // Update last login
                    var update = Builders<User>.Update.Set(u => u.last_login, DateTime.UtcNow);
                    await _mongoContext.Users.UpdateOneAsync(u => u._id == user._id, update);

                    // Store user info in session
                    HttpContext.Session.SetString("UserId", user._id);
                    HttpContext.Session.SetString("Username", user.username);
                    HttpContext.Session.SetString("UserRole", user.role ?? "user");
                    HttpContext.Session.SetString("ProfileImage", user.profile_image ?? "");
                    HttpContext.Session.SetString("UserEmail", user.email);

                    // Create claims for cookie authentication
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user._id),
                        new Claim(ClaimTypes.Name, user.username),
                        new Claim(ClaimTypes.Email, user.email),
                        new Claim(ClaimTypes.Role, user.role ?? "user")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("User {Username} logged in with role: {Role}", user.username, user.role);

                    TempData["SuccessMessage"] = $"Welcome back, {user.username}!";
                    return RedirectToAction("Home", "Home");
                }

                ModelState.AddModelError("", "Invalid email or password, or account is deactivated.");
                return View("_LoginModal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", email);
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View("_LoginModal");
            }
        }

        // GET: Register page
        [HttpGet]
        public IActionResult Register()
        {
            return View("_RegisterModal");
        }

        // POST: Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user)
        {
            _logger.LogInformation("Registration attempt for user: {Username}", user.username);

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if username already exists
                    var existingUserByUsername = await _mongoContext.Users
                        .Find(u => u.username == user.username)
                        .FirstOrDefaultAsync();

                    if (existingUserByUsername != null)
                    {
                        _logger.LogWarning("Registration failed - Username already exists: {Username}", user.username);
                        ModelState.AddModelError("username", "Username already exists");
                        return View("_RegisterModal", user);
                    }

                    // Check if email already exists
                    var existingUserByEmail = await _mongoContext.Users
                        .Find(u => u.email == user.email)
                        .FirstOrDefaultAsync();

                    if (existingUserByEmail != null)
                    {
                        _logger.LogWarning("Registration failed - Email already exists: {Email}", user.email);
                        ModelState.AddModelError("email", "Email already registered");
                        return View("_RegisterModal", user);
                    }

                    // Hash the password and set default values
                    user.password = HashPassword(user.password);
                    user.date_created = DateTime.UtcNow;
                    user._id = null;
                    user.role = "user";
                    user.is_active = true;
                    user.profile_image = "";
                    user.bio = "";
                    user.last_login = null;

                    await _mongoContext.Users.InsertOneAsync(user);

                    _logger.LogInformation("User registered successfully: {Username}", user.username);
                    TempData["SuccessMessage"] = "Registration successful! Please log in.";
                    return RedirectToAction("Home", "Home");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during registration for user: {Username}", user.username);
                    ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                    return View("_RegisterModal", user);
                }
            }

            return View("_RegisterModal", user);
        }

        // GET: Forgot Password page
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Please enter your email address." });

                    ModelState.AddModelError("", "Please enter your email address.");
                    return View();
                }

                // Check if user exists
                var user = await _mongoContext.Users
                    .Find(u => u.email == email && u.is_active)
                    .FirstOrDefaultAsync();

                if (user != null)
                {
                    // Generate a secure token
                    var token = GenerateSecureToken();

                    // Create password reset token document
                    var resetToken = new PasswordResetToken
                    {
                        Email = email,
                        Token = token,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddHours(1),
                        IsUsed = false,
                        UsedAt = null
                    };

                    // Save token to database
                    await _mongoContext.PasswordResetTokens.InsertOneAsync(resetToken);

                    // Generate reset link
                    var resetLink = Url.Action("ResetPassword", "Account",
                        new { email = email, token = token },
                        protocol: HttpContext.Request.Scheme);

                    // Send email using the service
                    try
                    {
                        await _emailService.SendPasswordResetEmailAsync(email, user.username, resetLink);
                        _logger.LogInformation("Password reset email sent to {Email}", email);
                    }
                    catch (EmailException ex)
                    {
                        // Log but continue - user will still get success message
                        _logger.LogWarning(ex, "Failed to send password reset email to {Email}", email);
                    }
                }
                else
                {
                    // Log for security monitoring
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", email);
                }

                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = "If an account exists with that email, you will receive password reset instructions shortly."
                    });
                }

                // Always show success message (for security - don't reveal if user exists)
                TempData["SuccessMessage"] = "If an account exists with that email, you will receive password reset instructions shortly.";
                return RedirectToAction("ForgotPassword");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing forgot password request for email: {Email}", email);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while processing your request. Please try again." });

                TempData["ErrorMessage"] = "An error occurred while processing your request. Please try again.";
                return View();
            }
        }

        // GET: Reset Password page
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string email, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Invalid password reset link.";
                    return RedirectToAction("ForgotPassword");
                }

                // Validate token
                var resetToken = await _mongoContext.PasswordResetTokens
                    .Find(t => t.Email == email && t.Token == token && !t.IsUsed)
                    .FirstOrDefaultAsync();

                if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
                {
                    TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
                    return RedirectToAction("ForgotPassword");
                }

                var viewModel = new ResetPasswordViewModel
                {
                    Email = email,
                    Token = token
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reset password page");
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return RedirectToAction("ForgotPassword");
            }
        }

        // POST: Reset Password - Step 2: Set new password
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Validate token again
                var resetToken = await _mongoContext.PasswordResetTokens
                    .Find(t => t.Email == model.Email && t.Token == model.Token && !t.IsUsed)
                    .FirstOrDefaultAsync();

                if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
                {
                    TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
                    return RedirectToAction("ForgotPassword");
                }

                // Find user
                var user = await _mongoContext.Users
                    .Find(u => u.email == model.Email && u.is_active)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ForgotPassword");
                }

                // Update password
                var hashedPassword = HashPassword(model.NewPassword);
                var update = Builders<User>.Update
                    .Set(u => u.password, hashedPassword);

                await _mongoContext.Users.UpdateOneAsync(
                    u => u._id == user._id,
                    update);

                // Mark token as used
                var tokenUpdate = Builders<PasswordResetToken>.Update
                    .Set(t => t.IsUsed, true)
                    .Set(t => t.UsedAt, DateTime.UtcNow);

                await _mongoContext.PasswordResetTokens.UpdateOneAsync(
                    t => t.Id == resetToken.Id,
                    tokenUpdate);

                // Clean up expired tokens
                await CleanupExpiredTokens();

                TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in with your new password.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email: {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred while resetting your password. Please try again.");
                return View(model);
            }
        }

        // Helper method to generate secure token
        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        private async Task CleanupExpiredTokens()
        {
            try
            {
                // Delete tokens that expired more than 24 hours ago
                var cutoffDate = DateTime.UtcNow.AddHours(-24);
                await _mongoContext.PasswordResetTokens.DeleteManyAsync(
                    t => t.ExpiresAt < cutoffDate || (t.IsUsed && t.UsedAt < cutoffDate));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserRecipes(string userId = null)
        {
            try
            {
                // If userId is not provided, use the current logged-in user
                if (string.IsNullOrEmpty(userId))
                {
                    userId = HttpContext.Session.GetString("UserId");

                    if (string.IsNullOrEmpty(userId))
                    {
                        return Json(new
                        {
                            success = true,
                            user = new { username = "User", recipe_count = 0 },
                            recipes = new List<object>()
                        });
                    }
                }

                // Get user's recipes (only public ones)
                var recipes = await _mongoContext.Recipes
                    .Find(r => r.user_id == userId && r.status == "public")
                    .SortByDescending(r => r.created_at)
                    .ToListAsync();

                // Get user info
                var user = await _mongoContext.Users
                    .Find(u => u._id == userId && u.is_active)
                    .FirstOrDefaultAsync();

                // Get like counts for each recipe
                var recipesWithDetails = new List<object>();
                foreach (var recipe in recipes)
                {
                    var likesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "like");
                    var dislikesCount = await _mongoContext.Votes
                        .CountDocumentsAsync(v => v.recipe_id == recipe.recipe_id && v.vote_type == "dislike");

                    recipesWithDetails.Add(new
                    {
                        recipe_id = recipe.recipe_id,
                        title = recipe.title,
                        description = recipe.description,
                        images = recipe.images,
                        category = recipe.category,
                        tags = recipe.tags,
                        created_at = recipe.created_at,
                        likes = (int)likesCount,
                        dislikes = (int)dislikesCount,
                        comments_count = recipe.comments_count
                    });
                }

                return Json(new
                {
                    success = true,
                    user = new
                    {
                        username = user?.username ?? "Unknown",
                        recipe_count = recipes.Count
                    },
                    recipes = recipesWithDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user recipes for userId: {UserId}", userId);
                return Json(new { success = false, message = "An error occurred while loading recipes." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Clear session
            HttpContext.Session.Clear();

            // Clear cookies
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "You have been logged out successfully.";

            return RedirectToAction("Home", "Home");
        }

        public IActionResult DebugSession()
        {
            var sessionInfo = new
            {
                UserId = HttpContext.Session.GetString("UserId"),
                Username = HttpContext.Session.GetString("Username"),
                UserRole = HttpContext.Session.GetString("UserRole"),
                ProfileImage = HttpContext.Session.GetString("ProfileImage"),
                SessionId = HttpContext.Session.Id
            };

            return Json(sessionInfo);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public IActionResult CheckAuth()
        {
            var authInfo = new
            {
                IsAuthenticated = User.Identity.IsAuthenticated,
                UserName = User.Identity.Name,
                Roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value),
                SessionUserId = HttpContext.Session.GetString("UserId"),
                SessionUserRole = HttpContext.Session.GetString("UserRole")
            };

            return Json(authInfo);
        }
    }

    // Custom exception for email errors
    public class EmailException : Exception
    {
        public EmailException(string message) : base(message) { }
        public EmailException(string message, Exception innerException) : base(message, innerException) { }
    }
}