namespace customer_support_service.Service
{
    public class RabbitMQSettings
    {
        public string HostName { get; set; } = "rabbitmq";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "admin";
        public string Password { get; set; } = "admin123";
        public string QuestionsQueue { get; set; } = "customer_questions";
        public string AnswersQueue { get; set; } = "customer_answers";
    }
}
