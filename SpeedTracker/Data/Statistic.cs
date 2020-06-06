using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SpeedTracker.Data
{
    public class Statistic
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DataType Type { get; set; } = DataType.Other;
        public double Value { get; set; } = -1;
    }

    public class Server
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Key]
        public int Id { get; set; }

        public string Host { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }
    }

    public class Event
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Title { get; set; } = "";
        public string Details { get; set; } = "";
    }

    public enum DataType
    {
        Other = 0,
        DownloadSpeed = 1,
        UploadSpeed = 2,
        Latancy = 3,
    }
}
