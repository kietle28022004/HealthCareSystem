using BusinessObjects.DataTransferObjects.PatientDTOs;
using Microsoft.AspNetCore.Mvc;
using Services.Interface;
using System.Security.Claims;
using System.Text.Json;

namespace HealthcareSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly IPatientService _patientService;
        private readonly ILogger<PatientController> _logger;

        public PatientController(IPatientService patientService, ILogger<PatientController> logger)
        {
            _patientService = patientService;
            _logger = logger;
        }

        // GET: api/patient/5
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetPatientProfile(int userId)
        {
            try
            {
                // Validate: Chỉ cho phép xem profile của chính mình (trừ khi là Admin/Doctor)
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != null && int.Parse(currentUserId) != userId)
                {
                    return Forbid();
                }

                var profile = await _patientService.GetPatientProfileAsync(userId);
                if (profile == null) return NotFound("Patient not found.");

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile {UserId}", userId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        // PUT: api/patient/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdatePatientProfile([FromBody] UpdatePatientProfileDTO dto)
        {
            if (dto == null) return BadRequest("Invalid data.");

            // Validate Security: User chỉ được sửa profile của chính mình
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId) || int.Parse(currentUserId) != dto.UserId)
            {
                return Unauthorized("You can only update your own profile.");
            }

            try
            {
                var success = await _patientService.UpdatePatientProfileAsync(dto);
                if (!success) return NotFound("Patient record not found.");

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile {UserId}", dto.UserId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        // POST: api/patient
        [HttpPost]
        public async Task<IActionResult> CreatePatientProfile([FromBody] CreatePatientDTO patientDto)
        {
            if (patientDto == null)
                return BadRequest("Invalid data.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var success = await _patientService.CreatePatientProfileAsync(patientDto);
                if (!success)
                {
                    _logger.LogWarning("Failed to create patient profile for userId {UserId}. Patient may already exist.", patientDto.UserId);
                    return BadRequest("Patient profile already exists or creation failed.");
                }

                _logger.LogInformation("Patient profile created successfully for userId {UserId}", patientDto.UserId);
                return Ok("Patient profile created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient profile for userId {UserId}", patientDto?.UserId);
                return StatusCode(500, "An error occurred while creating patient profile.");
            }
        }

        // PUT: api/patient/health
        [HttpPut("health")]
        public async Task<IActionResult> UpdatePatientHealthInfo([FromBody] UpdateHealthInfoDTO dto)
        {
            if (dto == null) return BadRequest("Invalid data.");

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId) || int.Parse(currentUserId) != dto.UserId)
            {
                return Unauthorized("You can only update your own health information.");
            }

            try
            {
                var success = await _patientService.UpdateHealthInfoAsync(dto);
                if (!success) return NotFound("Patient record not found for health update.");

                return Ok(new { message = "Health information updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating health info {UserId}", dto.UserId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UploadAvatar([FromForm] int userId, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please select an image file.");
            }

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId) || int.Parse(currentUserId) != userId)
            {
                return Unauthorized("You can only update your own avatar.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Unsupported image format. Use jpg, jpeg, png, gif, or webp.");
            }

            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest("Image size must be less than 5MB.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var avatarUrl = await _patientService.UploadAvatarAsync(userId, stream, file.FileName);
                if (string.IsNullOrWhiteSpace(avatarUrl))
                {
                    return NotFound("Patient not found.");
                }

                return Ok(new { message = "Avatar uploaded successfully", avatarUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar for user {UserId}", userId);
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
