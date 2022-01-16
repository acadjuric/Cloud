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
            try
            {
                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                using (var tx = this.state.CreateTransaction())
                {
                    foreach (var key in keys)
                    {
                        if (await uredjajiNaRemontu.ContainsKeyAsync(tx, key))
                        {
                            //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                            var device = await devices.TryGetValueAsync(tx, key);
                            if (device.HasValue)
                            {
                                device.Value.OnRemont = false;
                                await devices.SetAsync(tx, key, device.Value);
                            }

                            await uredjajiNaRemontu.TryRemoveAsync(tx, key);
                        }
                    }

                    await tx.CommitAsync();
                }
            }
            catch(Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<List<Remont>> GetAllRemonts()
        {
            try
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
            catch(Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<bool> SendToRemont(int id, double timeInWarehouse, double workHours)
        {
            try
            {
                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                using (var tx = this.state.CreateTransaction())
                {
                    //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                    var device = await devices.TryGetValueAsync(tx, id);

                    if (device.HasValue == false)
                        return false;

                    if (await uredjajiNaRemontu.ContainsKeyAsync(tx, id) || device.Value.OnRemont)
                    {
                        return false;
                    }

                    Remont remont = new Remont(id)
                    {
                        DeviceId = id,
                        DeviceName = device.Value.Name,
                        HoursInWarehouse = timeInWarehouse,
                        WorkHours = workHours,
                        NumberOfRemont = await uredjajiNaRemontu.GetCountAsync(tx) + 1,
                        SendToRemont = DateTime.Now,
                        TimeSpentInRemont = -1,
                    };

                    await uredjajiNaRemontu.AddAsync(tx, id, remont);

                    //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                    device.Value.OnRemont = true;
                    await devices.SetAsync(tx, id, device.Value);

                    await tx.CommitAsync();

                    await WriteToTable(remont);
                }


                return true;
            }
            catch(Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<bool> WriteToTable(Remont remont)
        {
            try
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
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<List<Device>> GetAllDevices()
        {
            try
            {
                List<Device> retVal = new List<Device>();
                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");
                using (var tx = this.state.CreateTransaction())
                {
                    var enumerator = (await devices.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while( await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        if(enumerator.Current.Value.OnRemont == false)
                        {
                            retVal.Add(enumerator.Current.Value);
                        }
                    }
                }

                return retVal;
            }
            catch( Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task WriteInitialDevicesToTable()
        {
            List<Device> uredjaji = new List<Device>()
            {
                new Device(1){  Id = 1, Name= "PLC 2000", OnRemont= false},
                new Device(2){  Id = 2, Name= "PLC 3017", OnRemont= false},
                new Device(3){  Id = 3, Name= "PLC 4023", OnRemont= false},
                new Device(4){  Id = 4, Name= "RTU 32", OnRemont= false},
                new Device(5){  Id = 5, Name= "RTU 16", OnRemont= false},
                new Device(6){  Id = 6, Name= "RTU 64", OnRemont= false},
                new Device(7){  Id = 7, Name= "IED 200", OnRemont= false},
                new Device(8){  Id = 8, Name= "IED 500", OnRemont= false},
                new Device(9){  Id = 9, Name= "IED 300", OnRemont= false},
                new Device(10){ Id =10, Name= "HUB SWITCH 256", OnRemont= false},
                new Device(11){ Id =11, Name= "HUB SWITCH 128", OnRemont= false},
                new Device(12){ Id =12, Name= "HUB SWITCH 512", OnRemont= false},
            };

            foreach (var item in uredjaji)
            {
                try
                {
                    TableOperation insert = TableOperation.Insert(item);
                    tabela.Execute(insert);
                }
                catch (StorageException ex)
                {
                    string exception = ex.Message;
                    continue;
                }

            }
        }

        
    }
}
