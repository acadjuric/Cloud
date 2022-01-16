using PrijemRemont;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Publisher
{
    [ServiceContract]
    public interface IPublisher
    {
        //Prva lista su aktivni podaci, a druga istorijski
        [OperationContract]
        Task<Tuple<List<Remont>, List<Remont>>> GetRemontAndHistoryRemont();

        [OperationContract]
        Task<List<Device>> GetDevices();
    }
}
