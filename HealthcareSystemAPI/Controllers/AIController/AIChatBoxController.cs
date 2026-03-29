using BusinessObjects.DataTransferObjects.AI;
using BusinessObjects.Domain;
using HealthCareSystemAPI.Mapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.Interface;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthCareSystem.Controllers
{
    [Route("api/chatbox")]
    [ApiController]
    public class AIChatBoxController : ControllerBase
    {
        private readonly IAiConversationService _aiConversationService;
        private readonly IAiMessageService _aiMessageService;
        private readonly IDoctorService _doctorService;
        private readonly ISpecialtyService _specialtyService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenAIOptions _groqOptions;
        private readonly ILogger<AIChatBoxController> _logger;

        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string SystemPrompt = @"Bạn là trợ lý y tế AI thân thiện. Hãy trả lời ngắn gọn các câu hỏi về sức khỏe, y tế, dinh dưỡng, tập luyện hoặc wellness. Không đưa ra lời khuyên y khoa chuyên môn, chỉ cung cấp thông tin chung. Nếu người dùng hỏi về triệu chứng, hãy gợi ý họ nên đặt lịch khám với bác sĩ chuyên khoa phù hợp.";

        public AIChatBoxController(
            IAiConversationService aiConversationService,
            IAiMessageService aiMessageService,
            IOptions<OpenAIOptions> groqOptions,
            IHttpClientFactory httpClientFactory,
            IDoctorService doctorService,
            ISpecialtyService specialtyService,
            ILogger<AIChatBoxController> logger)
        {
            _aiConversationService = aiConversationService;
            _aiMessageService = aiMessageService;
            _groqOptions = groqOptions.Value;
            _httpClientFactory = httpClientFactory;
            _doctorService = doctorService;
            _specialtyService = specialtyService;
            _logger = logger;
        }

        // --- Groq API DTOs ---
        private record GroqMessage(string role, string content);
        private record GroqRequest(string model, List<GroqMessage> messages);
        private record GroqChoice(int index, GroqDelta message, string finish_reason);
        private record GroqDelta(string role, string content);
        private record GroqResponse(string id, List<GroqChoice> choices);

        private async Task<string?> CallGroqApiAsync(List<GroqMessage> messages)
        {
            var apiKey = _groqOptions.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Groq API key is not configured");
                return null;
            }

            var requestBody = new GroqRequest(_groqOptions.Model ?? "llama-3.3-70b-versatile", messages);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.PostAsJsonAsync(GroqApiUrl, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Groq API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
                return null;
            }

            var groqResponse = await response.Content.ReadFromJsonAsync<GroqResponse>();
            return groqResponse?.choices?.FirstOrDefault()?.message?.content;
        }

        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] AiChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message is required!");

            if (request.UserId == 0)
                return Unauthorized("User is not logged in.");

            // 1. Get or create AI conversation
            var conversation = await _aiConversationService.GetConversationByUserId(request.UserId);
            if (conversation == null)
            {
                conversation = new Aiconversation
                {
                    UserId = request.UserId,
                    StartedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true
                };
                await _aiConversationService.CreateConversation(conversation);
            }

            // 2. Get last 10 messages for context
            var messages = await _aiMessageService.GetMessagesByUserId(request.UserId);
            var lastMessages = messages.OrderBy(m => m.SentAt).TakeLast(10).ToList();

            // 3. Build Groq messages
            var groqMessages = new List<GroqMessage>
            {
                new("system", SystemPrompt)
            };

            foreach (var msg in lastMessages)
            {
                var role = string.Equals(msg.Sender, "User", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant";
                groqMessages.Add(new(role, msg.Content));
            }
            groqMessages.Add(new("user", request.Message));

            // 4. Call Groq API
            string aiReply;
            DateTime aiSentAt = DateTime.Now;

            try
            {
                aiReply = await CallGroqApiAsync(groqMessages) ?? "Xin lỗi, tôi không thể kết nối đến dịch vụ AI. Vui lòng thử lại sau.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Groq API");
                aiReply = "Xin lỗi, tôi không thể kết nối đến dịch vụ AI. Vui lòng thử lại sau.";
            }

            // 5. Save messages to DB
            var userSentAt = aiSentAt.AddMilliseconds(-200);
            await SaveConversationMessages(request.UserId, conversation, request.Message, aiReply, userSentAt, aiSentAt);

            // 6. Doctor recommendation based on specialty keyword
            var specialtyVietnamese = SpecialtyMapper.GetSpecialty(request.Message);
            List<object>? recommendedDoctors = null;

            if (!string.IsNullOrWhiteSpace(specialtyVietnamese))
            {
                // Translate and search doctors
                var specialtyEnglish = TranslateSpecialty(specialtyVietnamese);
                if (!string.IsNullOrWhiteSpace(specialtyEnglish))
                {
                    var dbSpecialty = await _specialtyService.GetSpecialtyByName(specialtyEnglish);
                    if (dbSpecialty != null)
                    {
                        var docs = await _doctorService.GetBySpecialtyAsync(dbSpecialty.SpecialtyId);
                        recommendedDoctors = docs.Select(d => (object)new
                        {
                            UserId = d.UserId,
                            SpecialtyId = d.Specialty?.SpecialtyId,
                            fullName = d.User?.FullName ?? "Unknown",
                            avatar = d.User?.AvatarUrl ?? "",
                            specialty = specialtyVietnamese,
                            experience = d.Experience ?? ""
                        }).ToList();
                    }
                }
            }

            return Ok(new { aiReply, recommendedDoctors });
        }

        private string? TranslateSpecialty(string vietnamese)
        {
            // Simple hardcoded translation map for common specialties
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "tim mach", "Cardiology" },
                { "tim mạch", "Cardiology" },
                { "da liễu", "Dermatology" },
                { "da lieu", "Dermatology" },
                { "nhi khoa", "Pediatrics" },
                { "nhi", "Pediatrics" },
                { "noi khoa", "Internal Medicine" },
                { "ngoai khoa", "Surgery" },
                { "sản", "Obstetrics & Gynecology" },
                { "san", "Obstetrics & Gynecology" },
                { "thần kinh", "Neurology" },
                { "than kinh", "Neurology" },
                { "xương khớp", "Orthopedics" },
                { "xuong khop", "Orthopedics" },
                { "mắt", "Ophthalmology" },
                { "mat", "Ophthalmology" },
                { "tai mũi họng", "Otolaryngology" },
                { "tai mui hong", "Otolaryngology" },
                { "hô hấp", "Pulmonology" },
                { "ho hap", "Pulmonology" },
                { "tiêu hóa", "Gastroenterology" },
                { "tieu hoa", "Gastroenterology" },
                { "thận", "Nephrology" },
                { "than", "Nephrology" },
                { "nội tiết", "Endocrinology" },
                { "noi tiet", "Endocrinology" },
                { "ung thu", "Oncology" },
                { "ung thư", "Oncology" },
                { "tâm thần", "Psychiatry" },
                { "tam than", "Psychiatry" },
                { "dị ứng", "Dermatology" },
                { "di ung", "Dermatology" }
            };

            foreach (var kvp in map)
            {
                if (vietnamese.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        private async Task SaveConversationMessages(int userId, Aiconversation conversation, string userMessage, string aiMessage, DateTime userSentAt, DateTime aiSentAt)
        {
            await _aiMessageService.SaveMessage(new[]
            {
                new Aimessage
                {
                    UserId = userId,
                    Sender = "User",
                    Content = userMessage,
                    SentAt = userSentAt,
                    IsRead = true
                },
                new Aimessage
                {
                    UserId = userId,
                    Sender = "AI",
                    Content = aiMessage,
                    SentAt = aiSentAt,
                    IsRead = false
                }
            });

            conversation.UpdatedAt = aiSentAt;
            await _aiConversationService.UpdateConversation(conversation);
        }

        [HttpGet("messages/{userId}")]
        public async Task<IActionResult> GetMessages(int userId)
        {
            var messages = await _aiMessageService.GetMessagesByUserId(userId);
            return Ok(messages ?? new List<Aimessage>());
        }

        [HttpDelete("messages/delete/{userId}")]
        public async Task<IActionResult> DeleteHistory(int userId)
        {
            if (userId == 0)
                return BadRequest("Invalid User ID.");

            await _aiMessageService.DeleteMessageByConversationId(userId);
            var conversation = await _aiConversationService.GetConversationByUserId(userId);
            if (conversation != null)
            {
                conversation.IsActive = false;
                conversation.UpdatedAt = DateTime.Now;
                await _aiConversationService.UpdateConversation(conversation);
            }
            return Ok();
        }
    }
}
