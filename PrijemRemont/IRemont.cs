using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    [ServiceContract]
    public interface IRemont
    {

        [OperationContract]
        Task<bool> SendToRemont(int id, double timeInWarehouse, double workHours);

        [OperationContract]
        Task<bool> WriteToTable();
    }
}
