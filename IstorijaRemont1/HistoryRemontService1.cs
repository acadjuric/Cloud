using Microsoft.ServiceFabric.Data;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using PrijemRemont;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IstorijaRemont1
{
    public class HistoryRemontService1 : IHistoryRemont1
    {
        CloudTable tabela = null;

        public HistoryRemontService1(CloudTable table)
        {
            this.tabela = table;
        }

        public Task<List<Remont>> GetAllHisotryRemonts()
        {
            try
            {
                return Task.FromResult<List<Remont>>(
                    tabela.CreateQuery<Remont>().AsEnumerable<Remont>().Where(item => item.PartitionKey == "Remont" && item.TimeSpentInRemont != -1).ToList()
                    );
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task WriteHistoryRemontsToTable(Dictionary<int, Remont> uredjajiZaIstoriju)
        {
            try
            {
                TableBatchOperation mergeBatch = new TableBatchOperation();

                foreach (var item in uredjajiZaIstoriju)
                {
                    item.Value.ETag = "*";
                    mergeBatch.Merge(item.Value);
                    //TableOperation mergeOperation = TableOperation.Merge(item.Value);
                    //tabela.Execute(mergeOperation);
                }

                if (mergeBatch.Count > 0)
                    await tabela.ExecuteBatchAsync(mergeBatch);

                return;
            }
            catch (StorageException ex)
            {
                string a = ex.Message;
                return;
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }
    }
}
