using customer_support_service.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace customer_support_service.Service
{
    public class SupportAgent : ISupportAgent
    {
        private readonly ILogger<SupportAgent> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _openAiApiKey;

        public SupportAgent(ILogger<SupportAgent> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _openAiApiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI API Key not configured");
        }

        public async Task<ChatMessage> ProcessQuestion(ChatMessage question)
        {
            _logger.LogInformation("Processing question from user {UserId}", question.UserId);

            // Simulate processing time
            await Task.Delay(Random.Shared.Next(500, 1500));

            // Generate automated response
            var answer = await GenerateAnswer(question.Message);

            _logger.LogInformation("Generated answer for user {UserId}", question.UserId);

            return new ChatMessage
            {
                UserId = question.UserId,
                Message = answer,
                Timestamp = DateTime.UtcNow,
                Type = "answer"
            };
        }

        private async Task<string> GenerateAnswer(string question)
        {
            var lowerQuestion = question.ToLower();

            // Check predefined rules first
            if (lowerQuestion.Contains("hello") || lowerQuestion.Contains("hi"))
            {
                return "Hello! How can I assist you today?";
            }
            else if (lowerQuestion.Contains("price") || lowerQuestion.Contains("cost") || lowerQuestion.Contains("how much"))
            {
                return "Our pricing starts at $9.99/month for the basic plan, $19.99/month for professional, and $49.99/month for enterprise. Would you like more details about our plans?";
            }
            else if (lowerQuestion.Contains("shipping") || lowerQuestion.Contains("delivery"))
            {
                return "We offer free shipping on orders over $50. Standard delivery takes 3-5 business days, and express delivery takes 1-2 business days for an additional $9.99.";
            }
            else if (lowerQuestion.Contains("return") || lowerQuestion.Contains("refund"))
            {
                return "We accept returns within 30 days of purchase. Items must be in original condition. Refunds are processed within 5-7 business days after we receive the returned item.";
            }
            else if (lowerQuestion.Contains("support") || lowerQuestion.Contains("help") || lowerQuestion.Contains("contact"))
            {
                return "I'm here to help! You can ask me about pricing, shipping, returns, or any other questions. For complex issues, you can also email us at support@example.com or call 1-800-SUPPORT.";
            }
            else if (lowerQuestion.Contains("account") || lowerQuestion.Contains("login") || lowerQuestion.Contains("password"))
            {
                return "For account-related issues, you can reset your password using the 'Forgot Password' link on the login page. If you need further assistance, please contact our support team.";
            }
            else if (lowerQuestion.Contains("cancel") || lowerQuestion.Contains("subscription"))
            {
                return "You can cancel your subscription anytime from your account settings. There are no cancellation fees, and you'll continue to have access until the end of your billing period.";
            }
            else if (lowerQuestion.Contains("payment") || lowerQuestion.Contains("credit card"))
            {
                return "We accept all major credit cards, PayPal, and bank transfers. All payments are processed securely through our encrypted payment gateway.";
            }
            else if (lowerQuestion.Contains("thank"))
            {
                return "You're welcome! Is there anything else I can help you with?";
            }
            else if (lowerQuestion.Contains("bye") || lowerQuestion.Contains("goodbye"))
            {
                return "Thank you for contacting us! Have a great day!";
            }
            else
            {
                // No rule matched - use OpenAI API
                _logger.LogInformation("No rule matched, calling OpenAI API");
                return await GetOpenAiResponse(question);
            }
        }

        private async Task<string> GetOpenAiResponse(string question)
        {
            try
            {
                var request = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are a helpful customer support agent. Be concise, friendly, and professional. Provide accurate information about our services."
                        },
                        new
                        {
                            role = "user",
                            content = question
                        }
                    },
                    max_tokens = 300,
                    temperature = 0.7
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");

                var response = await _httpClient.PostAsJsonAsync(
                    "https://api.openai.com/v1/chat/completions",
                    request
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API call failed with status code: {StatusCode}", response.StatusCode);
                    return "I apologize, but I'm having trouble processing your request right now. Please try again later or contact our support team at support@example.com.";
                }

                var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
                var aiResponse = jsonResponse
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return aiResponse ?? "I apologize, but I couldn't generate a response. Please contact our support team.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return "I apologize, but I'm having trouble processing your request right now. Please try again later or contact our support team at support@example.com.";
            }
        }
    }
}