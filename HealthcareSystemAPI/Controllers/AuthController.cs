using BusinessObjects.DataTransferObjects.AuthDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.Interface;
using Services.Services;
using System.Security.Claims;

namespace HealthcareSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;

        public AuthController(IAuthService service)
        {
            _service = service;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _service.LoginAsync(request);

            if (result == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            return Ok(result);
        }

        [HttpPost("google")]
        public async Task<ActionResult<LoginResponse>> LoginGoogle([FromBody] LoginGoogle request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            Console.WriteLine("Google login successful for email: " + request.IdToken);

            var result = await _service.LoginGoogleAsync(request);

            if (result == null)
            {
                return Unauthorized(new { message = "Invalid email" });
            }
            
            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _service.RegisterAsync(request);

            if (result == null)
            {
                return BadRequest(new { message = "Email already exists or registration failed" });
            }

            return Ok(result);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { Success = true, Message = "Logout Successfully!!!" });
        }

        // GET: api/auth/me - Refresh session data (avatar, name, etc.)
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            if (!int.TryParse(userIdClaim, out var userId)) return Unauthorized();

            var result = await _service.GetLoginResponseAsync(userId);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
