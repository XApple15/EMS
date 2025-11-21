namespace device_service.Model
{
    public class Device
    {
        public Guid id { get; set; }
        public Guid? userId { get; set; }
        public string name { get; set; }
        public string consumption { get; set; }
    }
}
