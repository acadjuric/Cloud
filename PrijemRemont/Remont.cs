using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    public class Remont:TableEntity
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; }
        public double HoursInWarehouse { get; set; }
        public double WorkHours { get; set; }
        public long NumberOfRemont { get; set; }
        public DateTime SendToRemont { get; set; }
        public double TimeSpentInRemont { get; set; }

        public Remont(int id)
        {
            PartitionKey = "Remont";
            string rk = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " " + id.ToString();
            rk = rk.Replace(' ', '_').Replace('/','-');
            RowKey = rk;
        }

        public Remont()
        {

        }
    }
}
