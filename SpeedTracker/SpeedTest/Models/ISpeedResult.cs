namespace SpeedTest.Net.Models
{
    public interface ISpeedResult
    {
        Server Server { get; }
        double Speed { get; }
        string Source { get; }
    }
}