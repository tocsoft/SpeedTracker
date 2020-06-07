using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeedTracker
{
    public class Settings
    {
        public TimeSpan Frequancy { get; set; } = TimeSpan.FromSeconds(60);
    }
}
