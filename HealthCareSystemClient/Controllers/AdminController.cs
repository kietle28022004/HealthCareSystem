using Microsoft.AspNetCore.Mvc;
using System;

namespace HealthCareSystemClient.Controllers
{
    public class AdminController : Controller
    {
        private bool IsAdmin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");
            return userId != null && string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        public IActionResult Index()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Login");
            }

            ViewData["ActiveMenu"] = "Dashboard";
            return View();
        }

        public IActionResult Users()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Login");
            }

            ViewData["ActiveMenu"] = "UserManagement";
            return View();
        }

    }
}
