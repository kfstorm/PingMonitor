using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace Kfstorm.PingMonitor
{
    public class PingResult
    {
        public DateTime DateTime { get; set; }
        public TimeSpan TimeCost { get; set; }
        public IPStatus Status { get; set; }
    }
}
