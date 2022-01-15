using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace IstorijaRemont
{
    [ServiceContract]
    public interface IHistoryRemont
    {

        [OperationContract]
        Task<List<object>> GetAllHisotryRemonts();

        [OperationContract]
        Task WriteHistoryRemontsToTable();
    }
}
