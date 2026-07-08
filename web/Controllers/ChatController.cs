using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpotifyStatisticsWebApp.Data;
using SpotifyStatisticsWebApp.Services;
using System.Security.Claims;

namespace SpotifyStatisticsWebApp.Controllers
{
    // Only logged-in users can use chat.
    // Uses cookie auth from the website (not the iOS Bearer token).
    [Authorize]
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly OpenAIService _openAI;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public ChatController(
            OpenAIService openAI,
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager)
        {
            _openAI = openAI;
            _db = db;
            _userManager = userManager;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }

        public class ChatResponse
        {
            public string Reply { get; set; } = "";
        }

        // POST /api/chat
        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new ChatResponse { Reply = "Message is empty." });

            // Get CURRENT logged-in user id from auth.
            // Do not trust any "userId" coming from the chat text.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ChatResponse { Reply = "Not logged in." });

            // Load Identity user (for email, if needed in the assistant context)
            var user = await _userManager.FindByIdAsync(userId);

            // Load ONLY this user's profile from DB
            var profile = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            // Private context for the assistant.
            // The assistant must ONLY answer about "this user".
            var profileContext =
                "Current user profile (private, only for this authenticated user):\n" +
                $"- UserId: {userId}\n" +
                $"- Email: {user?.Email ?? "unknown"}\n" +
                $"- DisplayName: {profile?.DisplayName ?? "not set"}\n" +
                $"- PhoneNumber: {profile?.PhoneNumber ?? "not set"}\n" +
                "Rules: answer only about THIS user. If asked about another user, refuse.";

            var reply = await _openAI.AskAsync(
                userMessage: request.Message.Trim(),
                profileContext: profileContext);

            return Ok(new ChatResponse { Reply = reply });
        }
    }
}

