using MongoDB.Driver;
using Microsoft.AspNetCore.Mvc;

public class TestController : Controller
{
    public IActionResult MongoCheck()
    {
        try
        {
            var client = new MongoClient("mongodb+srv://onlinerecipesharing_db_user:IPT@onlinerecipesharing.rcfsxna.mongodb.net/");
            var dbList = client.ListDatabaseNames().ToList();
            return Content($"✅ MongoDB connected! Found {dbList.Count} databases.");
        }
        catch (Exception ex)
        {
            return Content($"❌ Connection failed: {ex.Message}");
        }
    }
}
