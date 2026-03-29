using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Twilio.Jwt.AccessToken; // ⬅️ Namespace quan trọng
using System.Collections.Generic;

[ApiController]
[Route("api/twilio")]
[Authorize] // Yêu cầu phải có JWT hợp lệ
public class TwilioController : ControllerBase
{
    private readonly string _accountSid;
    private readonly string _apiKeySid;
    private readonly string _apiSecret;

    public TwilioController(IConfiguration configuration)
    {
        var configuredAccountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
        var configuredApiKeySid = configuration["Twilio:ApiKeySid"]
                     ?? configuration["Twilio:ApiKey"]
                     ?? string.Empty;
        var configuredApiSecret = configuration["Twilio:ApiSecret"] ?? string.Empty;

        // Handle common misconfiguration where AccountSid and ApiKeySid are swapped.
        if (configuredAccountSid.StartsWith("SK") && configuredApiKeySid.StartsWith("AC"))
        {
            _accountSid = configuredApiKeySid;
            _apiKeySid = configuredAccountSid;
        }
        else
        {
            _accountSid = configuredAccountSid;
            _apiKeySid = configuredApiKeySid;
        }

        _apiSecret = configuredApiSecret;
    }

    [HttpGet("token")]
    public IActionResult GetVideoToken([FromQuery] string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            return BadRequest(new { message = "Tên phòng (roomName) là bắt buộc." });
        }

        if (string.IsNullOrWhiteSpace(_accountSid) || string.IsNullOrWhiteSpace(_apiKeySid) || string.IsNullOrWhiteSpace(_apiSecret))
        {
            return StatusCode(500, new
            {
                message = "Twilio configuration is missing.",
                required = new[]
                {
                    "Twilio:AccountSid (must start with AC)",
                    "Twilio:ApiKeySid (must start with SK)",
                    "Twilio:ApiSecret"
                }
            });
        }

        if (!_accountSid.StartsWith("AC") || !_apiKeySid.StartsWith("SK"))
        {
            return StatusCode(500, new
            {
                message = "Twilio configuration is invalid.",
                hint = "AccountSid must start with AC, ApiKeySid must start with SK. Create an API Key in Twilio Console and use its SID + Secret."
            });
        }

        // Lấy User ID (danh tính) từ JWT Token
        var identity = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(identity))
        {
            return Unauthorized("Danh tính user không tìm thấy trong token.");
        }

        try
        {
            var grant = new VideoGrant { Room = roomName };
            var grants = new HashSet<IGrant> { grant };

            var token = new Token(
                _accountSid,
                _apiKeySid,
                _apiSecret,
                identity: identity,
                grants: grants
            );

            return Ok(new
            {
                token = token.ToJwt(),
                room = roomName
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi nội bộ khi tạo token Twilio: {ex.Message}");
        }
    }
}