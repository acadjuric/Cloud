using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    public class Device:TableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool OnRemont { get; set; }


        public Device(int id)
        {
            PartitionKey = "Device";
            RowKey = id.ToString();
        }


        public Device()
        {

        }
    }
}
