using Microsoft.AspNetCore.Mvc;
using OnlineRecipeSharing.Models;
using OnlineRecipeSharing.Models.ViewModels;
using OnlineRecipeSharing.Data;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.ComponentModel.DataAnnotations;
using OnlineRecipeSharing.Models.OnlineRecipeSharing.Models;

namespace OnlineRecipeSharing.Controllers
{
    public class ReportsController : Controller
    {
        private readonly MongoDBContext _mongoContext;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(MongoDBContext mongoContext, ILogger<ReportsController> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        // POST: Reports/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string recipeId, string reason, string description = "")
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Please log in to report recipes." });
                }

                if (string.IsNullOrEmpty(recipeId) || string.IsNullOrEmpty(reason))
                {
                    return Json(new { success = false, message = "Recipe ID and reason are required." });
                }

                // Check if recipe exists using async
                var recipeExists = await _mongoContext.Recipes
                    .Find(r => r.recipe_id == recipeId)
                    .AnyAsync();

                if (!recipeExists)
                {
                    return Json(new { success = false, message = "Recipe not found." });
                }

                // Check if user already reported this recipe using async
                var alreadyReported = await _mongoContext.Reports
                    .Find(r => r.RecipeId == recipeId && r.ReportedBy == userId && r.status == "pending")
                    .AnyAsync();

                if (alreadyReported)
                {
                    return Json(new { success = false, message = "You have already reported this recipe." });
                }

                // Create new report
                var report = new Report
                {
                    RecipeId = recipeId,
                    ReportedBy = userId,
                    Reason = reason.Trim(),
                    description = description?.Trim() ?? "",
                    status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _mongoContext.Reports.InsertOneAsync(report);

                _logger.LogInformation("Report created for recipe {RecipeId} by user {UserId}", recipeId, userId);

                return Json(new { success = true, message = "Recipe reported successfully. Our team will review it shortly." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating report for recipe {RecipeId}", recipeId);
                return Json(new { success = false, message = "An error occurred while reporting the recipe." });
            }
        }

    }
}