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
                    NumberOfRemont = await uredjajiNaRemontu.GetCountAsync(tx)  + 1,
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
            catch(StorageException ex)
            {
                //The remote server returned an error: (409) Conflict.
                string a = ex.Message;
                return false;
            }

            return true;
        }
    }
}
