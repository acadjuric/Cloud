using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using PrijemRemont;

namespace IstorijaRemont1
{
    [ServiceContract]
    public interface IHistoryRemont1
    {
        [OperationContract]
        Task<List<Remont>> GetAllHisotryRemonts();

        [OperationContract]
        Task WriteHistoryRemontsToTable(Dictionary<int,Remont> uredjajiZaIstoriju);
    }
}
