using customer_support_service.Model;

namespace customer_support_service.Service
{
    public class SupportAgent : ISupportAgent
    {
        private readonly ILogger<SupportAgent> _logger;

        public SupportAgent(ILogger<SupportAgent> logger)
        {
            _logger = logger;
        }

        public async Task<ChatMessage> ProcessQuestion(ChatMessage question)
        {
            _logger.LogInformation("Processing question from user {UserId}", question.UserId);

            // Simulate processing time
            await Task.Delay(Random.Shared.Next(500, 1500));

            // Generate automated response
            var answer = GenerateAnswer(question.Message);

            _logger.LogInformation("Generated answer for user {UserId}", question.UserId);

            return new ChatMessage
            {
                UserId = question.UserId,
                Message = answer,
                Timestamp = DateTime.UtcNow,
                Type = "answer"
            };
        }

        private string GenerateAnswer(string question)
        {
            var lowerQuestion = question.ToLower();

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
                return $"Thank you for your question. I've received: '{question}'. While I can help with common questions about pricing, shipping, returns, and account management, your specific question may require a human agent. Would you like me to connect you with a support specialist?";
            }
        }
    }
}
