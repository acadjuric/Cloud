using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrijemRemont
{
    public class RemontService : IRemont
    {
        private IReliableStateManager state = null;

        public RemontService(IReliableStateManager state)
        {
            this.state = state;
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

                Remont remont = new Remont()
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
                return true;
            }
        }

        public async Task<bool> WriteToTable()
        {
            throw new NotImplementedException();
        }
    }
}
