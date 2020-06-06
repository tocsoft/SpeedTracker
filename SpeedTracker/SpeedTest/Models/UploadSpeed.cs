using SpeedTest.Net.Enums;
using SpeedTest.Net.Helpers;

namespace SpeedTest.Net.Models
{
    public class UploadSpeed : ISpeedResult
    {
        public Server Server { get; internal set; }
        public double Speed { get; internal set; }
        public string Source { get; set; }

        public override string ToString()
        {
            var test = Speed / 1024;
            if (test / 1024 < 1)
            {
                return ToString(SpeedTestUnit.KiloBitsPerSecond);
            }
            return ToString(SpeedTestUnit.MegaBitsPerSecond);
        }

        public string ToString(SpeedTestUnit unit)
        {
            return $"{Speed.FromBytesPerSecondTo(unit)}{unit.ToShortIdentifier()} Up ({Server.Host})";
        }
    }
}