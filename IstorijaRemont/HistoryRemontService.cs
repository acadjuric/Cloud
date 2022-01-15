using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using PrijemRemont;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IstorijaRemont
{
    public class HistoryRemontService : IHistoryRemont
    {
        IReliableStateManager state = null;
        CloudTable tabela = null;

        public HistoryRemontService(IReliableStateManager state, CloudTable table)
        {
            this.state = state;
            this.tabela = table;
        }

        public Task<List<object>> GetAllHisotryRemonts()
        {
            throw new NotImplementedException();
        }

        public async Task WriteHistoryRemontsToTable()
        {
            var uredjajiZaIstorjiu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontsForHistory");

            using (var tx = this.state.CreateTransaction())
            {
                var enumerator = (await uredjajiZaIstorjiu.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    try
                    {
                        TableOperation mergeOperation = TableOperation.Merge(enumerator.Current.Value);
                        tabela.Execute(mergeOperation);
                    }
                    catch (StorageException ex)
                    {
                        string a = ex.Message;
                        continue;
                    }
                }

                await uredjajiZaIstorjiu.ClearAsync();
                await tx.CommitAsync();
            }
        }
    }
}
