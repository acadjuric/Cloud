using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace PrijemRemont
{
    public class RemontService : IRemont
    {
        private IReliableStateManager state = null;
        private CloudTable tabela = null;
        public RemontService(IReliableStateManager state, CloudTable tabela)
        {
            this.state = state;
            this.tabela = tabela;
        }

        public async Task DeleteHistoryRemontsFromCurrentRemonts(List<int> keys)
        {
            var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

            using (var tx = this.state.CreateTransaction())
            {
                foreach (var key in keys)
                {
                    if (await uredjajiNaRemontu.ContainsKeyAsync(tx, key))
                    {
                        await uredjajiNaRemontu.TryRemoveAsync(tx, key);
                    }
                }

                await tx.CommitAsync();
            }
        }

        public async Task<List<Remont>> GetAllRemonts()
        {
            List<Remont> retVal = new List<Remont>();

            var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");
            using (var tx = this.state.CreateTransaction())
            {
                var enumerator = (await uredjajiNaRemontu.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    retVal.Add(enumerator.Current.Value);
                }
            }

            return retVal;
        }

        public async Task<bool> SendToRemont(int id, double timeInWarehouse, double workHours)
        {
            var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

            using (var tx = this.state.CreateTransaction())
            {
                if (await uredjajiNaRemontu.ContainsKeyAsync(tx, id))
                {
                    return false;
                }

                Remont remont = new Remont(id)
                {
                    DeviceId = id,
                    HoursInWarehouse = timeInWarehouse,
                    WorkHours = workHours,
                    NumberOfRemont = await uredjajiNaRemontu.GetCountAsync(tx) + 1,
                    SendToRemont = DateTime.Now,
                    TimeSpentInRemont = -1,
                };

                await uredjajiNaRemontu.AddAsync(tx, id, remont);

                await tx.CommitAsync();

                await WriteToTable(remont);
            }


            return true;
        }

        public async Task<bool> WriteToTable(Remont remont)
        {
            TableOperation addRemont = TableOperation.Insert(remont);
            try
            {
                var a = await tabela.ExecuteAsync(addRemont);
            }
            catch (StorageException ex)
            {
                //The remote server returned an error: (409) Conflict.
                string a = ex.Message;
                return false;
            }

            return true;
        }
    }
}
