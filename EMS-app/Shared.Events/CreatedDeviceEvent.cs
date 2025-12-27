using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Events
{
    public  class CreatedDeviceEvent
    {
        public string id { get; set; } = string.Empty;
        public string Consumption { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}
